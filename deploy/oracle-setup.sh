#!/usr/bin/env bash
# Oracle Cloud'daki Ubuntu sunucuyu bir kez hazırlar: güvenlik duvarında 80/443'ü açar, Docker'ı kurar.
# Kullanım (sunucuda, normal kullanıcıyla; "ubuntu"):   bash deploy/oracle-setup.sh
#
# Önemli: Bu betik sunucunun İÇİNDEKİ duvarı açar. Oracle panelindeki ağ kuralını
# (VCN → Security List → Ingress Rules: 80 ve 443) ayrıca sen eklemelisin; bkz. DEPLOY-ORACLE.md.
set -euo pipefail

if [[ "$(id -u)" == "0" ]]; then
  echo "Bu betiği root olarak değil, normal kullanıcıyla çalıştır (gerektiğinde sudo ister)." >&2
  exit 1
fi

. /etc/os-release
if [[ "${ID:-}" != "ubuntu" ]]; then
  echo "Bu betik Ubuntu içindir (bu sunucu: ${PRETTY_NAME:-bilinmiyor})." >&2
  echo "Sunucuyu Oracle'da 'Canonical Ubuntu 24.04' imajıyla yeniden kur ya da adımları DEPLOY-ORACLE.md'den elle yap." >&2
  exit 1
fi

# ---------- 1) Güvenlik duvarı (iptables) ----------
# Oracle'ın Ubuntu imajı, 22 dışındaki gelen bağlantıları listenin sonundaki bir REJECT kuralıyla reddeder.
# Kuralımızı o REJECT'in ÖNÜNE koyarız (sonuna eklenen kural hiç çalışmaz).
# Docker kurulmadan ÖNCE yapıyoruz ki kaydedilen kurallara Docker'ın kendi zincirleri karışmasın.
firewall_changed=0

open_port() {
  local proto="$1" port="$2" line
  if sudo iptables -C INPUT -p "$proto" --dport "$port" -j ACCEPT 2>/dev/null; then
    return
  fi
  line="$(sudo iptables -L INPUT --line-numbers -n | awk '$2 == "REJECT" { print $1; exit }')"
  if [[ -n "$line" ]]; then
    sudo iptables -I INPUT "$line" -p "$proto" --dport "$port" -j ACCEPT
  else
    sudo iptables -A INPUT -p "$proto" --dport "$port" -j ACCEPT
  fi
  firewall_changed=1
}

echo "==> Güvenlik duvarı: 80/tcp, 443/tcp, 443/udp (HTTP/3) açılıyor"
open_port tcp 80
open_port tcp 443
open_port udp 443

if [[ "$firewall_changed" == "1" ]]; then
  if ! command -v netfilter-persistent >/dev/null 2>&1; then
    echo iptables-persistent iptables-persistent/autosave_v4 boolean true | sudo debconf-set-selections
    echo iptables-persistent iptables-persistent/autosave_v6 boolean true | sudo debconf-set-selections
    sudo DEBIAN_FRONTEND=noninteractive apt-get update -y
    sudo DEBIAN_FRONTEND=noninteractive apt-get install -y iptables-persistent
  fi
  sudo netfilter-persistent save   # yeniden başlatınca kurallar kaybolmasın
fi

# ---------- 2) Docker ----------
if command -v docker >/dev/null 2>&1; then
  echo "==> Docker zaten kurulu: $(docker --version)"
else
  echo "==> Docker kuruluyor (resmî kurulum betiği; ARM64 ve AMD64 için çalışır)"
  curl -fsSL https://get.docker.com | sudo sh
fi

if ! id -nG "$USER" | tr ' ' '\n' | grep -qx docker; then
  sudo usermod -aG docker "$USER"
  echo
  echo "Docker grubuna eklendin. DEĞİŞİKLİK İÇİN OTURUMU KAPAT-AÇ:  exit  →  tekrar ssh ile bağlan."
fi

# ---------- 3) git (depoyu çekmek için) ----------
command -v git >/dev/null 2>&1 || sudo DEBIAN_FRONTEND=noninteractive apt-get install -y git

echo
echo "Hazır. Sıradaki adım: DEPLOY-ORACLE.md → '4. Uygulamayı ayağa kaldır'."
