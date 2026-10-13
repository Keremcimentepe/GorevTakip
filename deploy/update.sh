#!/usr/bin/env bash
# Sunucuda uygulamayı güncelleyip yeniden başlatır: en son kodu çeker, imajları derler, konteynerleri yeniler,
# sonra canlı adreste /api/health'in cevap verdiğini doğrular.
# Kullanım (sunucuda):   bash deploy/update.sh
# İlk kurulumda da aynı betik kullanılır (.env hazırsa).
set -euo pipefail

cd "$(dirname "$0")/.."

if [[ ! -f .env ]]; then
  echo "HATA: .env yok. Önce:  cp deploy/env.prod.example .env  ve değerleri doldur." >&2
  exit 1
fi

# .env'den SITE_ADDRESS'i oku (betik .env'i çalıştırmaz, yalnızca bu satırı okur)
SITE_ADDRESS="$(sed -n 's/^SITE_ADDRESS=//p' .env | head -n1 | tr -d "\r\"' ")"
if [[ -z "$SITE_ADDRESS" ]]; then
  echo "HATA: .env içinde SITE_ADDRESS boş." >&2
  exit 1
fi

COMPOSE=(docker compose -f docker-compose.prod.yml)

if [[ -d .git ]]; then
  echo "==> Kod çekiliyor"
  git pull --ff-only
fi

echo "==> İmajlar derleniyor ve konteynerler yenileniyor"
"${COMPOSE[@]}" up -d --build --remove-orphans

echo "==> Eski imajlar temizleniyor"
docker image prune -f >/dev/null

# İlk kurulumda Caddy sertifikayı şimdi alır (birkaç saniye-1 dakika); API de migration uygular.
echo "==> https://$SITE_ADDRESS/api/health bekleniyor"
for attempt in $(seq 1 45); do
  if curl -fsS -o /dev/null -m 5 "https://$SITE_ADDRESS/api/health" 2>/dev/null; then
    echo "✓ Canlı: https://$SITE_ADDRESS"
    exit 0
  fi
  sleep 2
done

echo "✗ 90 saniyede cevap gelmedi. Günlükler:" >&2
"${COMPOSE[@]}" logs --tail=40 caddy api >&2
exit 1
