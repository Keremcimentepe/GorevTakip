# Yayına alma: Oracle Cloud sunucusu (kendi VDS'in)

[`DEPLOY.md`](DEPLOY.md) uygulamayı Render + Neon'a koyar. Bu rehber aynı uygulamayı **tek bir Oracle Cloud sunucusuna** koyar; sunucu 7/24 açık kalır, uyumaz, veritabanı da aynı makinededir.

```
 Tarayıcı ──https──►  Caddy (80/443, sertifikayı kendisi alır)
                         ├─ /api/*  ──►  API (.NET)  ──►  PostgreSQL
                         └─ geri kalan ─►  Arayüz (nginx, statik dosyalar)
```

Hepsi `docker-compose.prod.yml` ile tek komutla kalkar. Dışarıya yalnızca 80 ve 443 açıktır; veritabanı ve API sunucunun dışından erişilemez. Arayüz ve API aynı adreste olduğu için CORS ayarıyla uğraşmazsın.

## Önce bilmen gerekenler (Oracle'a özel)

- **Sunucun büyük olasılıkla ARM işlemcili** (`VM.Standard.A1.Flex`; Oracle'ın [ücretsiz katman belgesi](https://docs.oracle.com/en-us/iaas/Content/FreeTier/resourceref.htm) tüm hesaplar için bunu 2 OCPU + 12 GB olarak yazıyor). Sorun değil: kullandığımız imajların hepsi ARM64 sürümü sunuyor ve imajları **sunucunun kendisinde derliyoruz** (GitHub'dan hazır imaj çekmiyoruz).
- **Oracle, boşta duran ücretsiz sunucuları geri alabilir.** Aynı belgeye göre 7 gün boyunca CPU (95. yüzdelik) %20'nin, ağ %20'nin ve (A1'de) bellek %20'nin altında kalırsa sunucu "boşta" sayılır ve Oracle onu geri alabilir ("may" diyor; ne zaman ve nasıl bildirdiğini belge yazmıyor). Küçük bir uygulama tam bu kategoriye girer. Yani **sunucunun silinebileceğini varsay** ve düzenli yedek al (bkz. "Yedek"). Hesabı Pay As You Go'ya yükseltmenin bunu engelleyip engellemediğini belgede bulamadım; Oracle destek sayfasından teyit et.
- **Ücretsiz alan adı yoksa** `sslip.io` hilesini kullanacağız (aşağıda); kendi alan adın varsa daha sağlam.

## 0. Bilgisayarında (bir kez)

[`DEPLOY.md` → 0.1](DEPLOY.md) ve 0.2'yi yap: `Migrations/` klasörünü üret ve test projesini ekle. Sonra her şeyi **GitHub'a gönder** (`git push`). Sunucu kodu GitHub'dan çekecek; `Migrations/` orada yoksa API açılışta durur.

## 1. Oracle panelinde: ağ kuralı (en sık unutulan adım)

Oracle'da sunucu varsayılan olarak yalnızca SSH (22) kabul eder. 80 ve 443'ü sen açmalısın:

1. Menü → **Compute → Instances →** sunucun → **Primary VNIC** altında **Subnet** bağlantısı.
2. **Default Security List for …** → **Security rules → Add Ingress Rules**.
3. Bir kural ekle: **Source CIDR** `0.0.0.0/0`, **IP Protocol** TCP, **Destination Port Range** `80`. "Stateless" kutusunu **boş bırak**.
4. Aynısını `443` için tekrarla. (İstersen UDP `443` de ekle; HTTP/3 içindir, olmasa da site çalışır.)

Sunucunun **Public IP** adresini de not et (Instance sayfasında).

## 2. Alan adı

Caddy'nin HTTPS sertifikası alabilmesi için sitenin bir alan adı olmalı.

- **Alan adın yoksa (ücretsiz):** IP'nin noktalarını tire yap ve sonuna `.sslip.io` ekle. IP `129.151.10.20` ise adres `129-151-10-20.sslip.io` olur; bu ad kendiliğinden o IP'ye çözülür, kayıt gerekmez. Pratik ama adın kontrolü sende değil; ciddiye bindirirsen kendi alan adına geç.
- **Alan adın varsa:** DNS panelinde sunucunun genel IP'sine bir **A kaydı** ekle (ör. `gorevtakip.ornek.com`). Yayılması birkaç dakika sürebilir.

## 3. Sunucuya bağlan ve hazırla

Panelde sunucuyu kurarken indirdiğin özel anahtar dosyasıyla (Windows'ta PowerShell'de de çalışır):

```bash
ssh -i C:\yol\anahtar.key ubuntu@<GENEL-IP>
```

Windows "UNPROTECTED PRIVATE KEY FILE" hatası verirse anahtar dosyasının Özellikler → Güvenlik bölümünden yalnızca kendi kullanıcına izin bırak. Sunucu **Ubuntu** imajıyla kurulmuş olmalı (kullanıcı adı `ubuntu`). Oracle Linux gibi başka bir imaj seçtiysen sunucuyu Ubuntu 24.04 ile yeniden kur; betikler Ubuntu içindir.

Depoyu çek (aşağıdaki iki yoldan **biri**):

**Depo herkese açıksa:**

```bash
sudo apt-get update && sudo apt-get install -y git
git clone https://github.com/Keremcimentepe/GorevTakip.git
```

**Depo özelse** (sunucuya yalnızca-okuma "deploy key" veriyoruz):

```bash
sudo apt-get update && sudo apt-get install -y git
ssh-keygen -t ed25519 -f ~/.ssh/gorevtakip_deploy -N ""
cat ~/.ssh/gorevtakip_deploy.pub        # çıkan satırı kopyala
```

GitHub'da depo → **Settings → Deploy keys → Add deploy key** → yapıştır (**"Allow write access" işaretli olmasın**). Sonra:

```bash
git clone -c core.sshCommand="ssh -i ~/.ssh/gorevtakip_deploy -o IdentitiesOnly=yes" git@github.com:Keremcimentepe/GorevTakip.git
```

Sunucuyu hazırlayan betik (güvenlik duvarında 80/443'ü açar, Docker'ı kurar):

```bash
cd GorevTakip
bash deploy/oracle-setup.sh
exit                 # Docker grubunun geçerli olması için oturumu kapat...
```

...ve tekrar `ssh` ile bağlanıp `cd GorevTakip` yap.

## 4. Uygulamayı ayağa kaldır

```bash
cp deploy/env.prod.example .env
nano .env            # SITE_ADDRESS, POSTGRES_PASSWORD, JWT_KEY doldur (Ctrl+O kaydet, Ctrl+X çık)
```

Parola ve anahtarı sunucuda üretmek için (çıkan değeri `.env`'e yapıştır):

```bash
openssl rand -hex 24     # POSTGRES_PASSWORD için
openssl rand -hex 48     # JWT_KEY için
```

Sonra:

```bash
bash deploy/update.sh
```

Betik imajları derler (ARM sunucuda ilk seferde birkaç dakika), konteynerleri başlatır ve `https://<adresin>/api/health` cevap verene kadar bekler. Sertifika ilk açılışta alınır. `✓ Canlı:` görünce tarayıcıdan `https://<adresin>` adresini aç, hesap oluştur.

## 5. Doğrula

Kendi bilgisayarından (Git Bash ya da WSL):

```bash
bash scripts/smoke-test.sh https://<adresin>/api https://<adresin>
```

Kayıt, giriş, yetkisiz erişim, iki kullanıcının birbirinin verisini görememesi ve CORS gerçek canlı sistemde denenir.

## 6. Güncelleme

Kodu GitHub'a gönderdikten sonra sunucuda:

```bash
cd ~/GorevTakip
bash deploy/update.sh
```

`git pull`, yeniden derleme, yeniden başlatma ve sağlık kontrolü tek komutta. Veritabanı şeması değiştiyse (yeni migration) API açılışta kendisi uygular. Bu yöntem GitHub Actions'a bağlı değildir: testler yeşil olmadan da güncelleyebilirsin, o yüzden önce Actions'ın yeşil olduğunu kontrol et. (Güncellemeyi Actions'tan otomatik tetiklemek istersen bu ayrı bir adımdır.)

## 7. Yedek

Veritabanı sunucudaki bir Docker biriminde durur. Elle yedek:

```bash
bash deploy/backup.sh      # ~/gorevtakip-yedek/ içine .sql.gz yazar, son 14 yedeği tutar
```

Her gece 03:00'te otomatik için `crontab -e` aç ve şu satırı ekle:

```
0 3 * * * /home/ubuntu/GorevTakip/deploy/backup.sh >> /home/ubuntu/gorevtakip-yedek/cron.log 2>&1
```

**Yedek aynı sunucuda durur; sunucu silinirse yedek de gider.** Ara sıra kendi bilgisayarına indir (kendi bilgisayarında):

```bash
scp -i C:\yol\anahtar.key "ubuntu@<GENEL-IP>:gorevtakip-yedek/*.sql.gz" .
```

Geri yükleme (boş bir veritabanına; bu adımı gerçek bir ihtiyaç çıkmadan önce yeni kurulmuş bir sunucuda bir kez dene):

```bash
cd ~/GorevTakip
docker compose -f docker-compose.prod.yml stop api
docker compose -f docker-compose.prod.yml exec -T db sh -c 'dropdb -U "$POSTGRES_USER" "$POSTGRES_DB" && createdb -U "$POSTGRES_USER" "$POSTGRES_DB"'
gunzip -c ~/gorevtakip-yedek/<DOSYA>.sql.gz | docker compose -f docker-compose.prod.yml exec -T db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
docker compose -f docker-compose.prod.yml start api
```

---

## Günlük komutlar

```bash
cd ~/GorevTakip
docker compose -f docker-compose.prod.yml ps                 # çalışan konteynerler
docker compose -f docker-compose.prod.yml logs -f api        # API günlüğü (Ctrl+C çıkar)
docker compose -f docker-compose.prod.yml logs --tail=50 caddy
docker compose -f docker-compose.prod.yml restart api
```

**`docker compose down -v` KULLANMA:** `-v` veritabanı birimini siler, tüm kullanıcılar ve görevler gider. `down` (v'siz) güvenlidir.

## Sorun giderme

| Belirti | Neden / çözüm |
|---|---|
| Tarayıcı "bağlantı zaman aşımı" veriyor | En sık: 1. adımdaki Oracle ağ kuralı (80/443) eksik. Sonra sunucuda dene: `curl -I http://localhost` cevap veriyorsa sunucu iyi, sorun Oracle ağ kuralındadır. `sudo iptables -L INPUT -n --line-numbers` içinde 80/443 `ACCEPT` satırları, `REJECT`'ten **önce** olmalı. |
| `update.sh` "90 saniyede cevap gelmedi" diyor | `docker compose -f docker-compose.prod.yml logs --tail=50 caddy api` bak. Sertifika sorunu mu, API düşüyor mu, aşağıya bak. |
| Caddy günlüğünde sertifika/ACME hatası | Alan adı sunucunun IP'sine çözülmüyor ya da 80 portuna dışarıdan ulaşılamıyor. `nslookup <adres>` doğru IP'yi vermeli. Let's Encrypt'in deneme sınırı var (aynı ad için saatte 5 başarısız doğrulama): hatayı düzeltmeden tekrar tekrar deneme, bir süre bekle. |
| API sürekli yeniden başlıyor, günlükte "Hiç migration bulunamadı" | `GorevTakip/Migrations/` GitHub'a gönderilmemiş ([`DEPLOY.md` → 0.1](DEPLOY.md)). Gönder, sunucuda `bash deploy/update.sh`. |
| Günlükte "Jwt:Key" hatası | `.env` içindeki `JWT_KEY` boş ya da 32 karakterden kısa. Düzeltip `bash deploy/update.sh`. |
| Günlükte `password authentication failed` | `POSTGRES_PASSWORD`'ü veritabanı ilk oluştuktan sonra değiştirdin; Postgres parolayı yalnızca **ilk** başlatmada okur. Eski parolayı geri yaz (ya da verinin gitmesini göze alıp `docker compose … down -v`). |
| `permission denied … docker.sock` | Oturumu kapatıp yeniden bağlan (docker grubu). |
| Herkes çıkış yapmış göründü | `JWT_KEY` değişti; beklenen davranış. |
| Giriş ekranında "Çok fazla deneme" | Giriş/kayıt IP başına dakikada 10 istekle sınırlı; bir dakika bekle. |

## Güvenlik notları

- Sunucuya SSH anahtarıyla gir ve anahtar dosyasını kimseyle paylaşma; kaybedersen Oracle panelinden yeni bir anahtar tanımlaman gerekir.
- `.env` dosyası sunucudan çıkmasın, GitHub'a girmesin (`.gitignore`'da).
- Veritabanı portu dışarıya açık değildir: `docker-compose.prod.yml` içinde `ports:` yoktur. Eklemeyin.
- Sunucu paketlerini ara sıra güncelle: `sudo apt-get update && sudo apt-get upgrade -y`.
