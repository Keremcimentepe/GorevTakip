# Yayına alma rehberi (Neon + Render)

> Kendi sunucun (ör. Oracle Cloud VDS) varsa bunun yerine [`DEPLOY-ORACLE.md`](DEPLOY-ORACLE.md) rehberini izle.

Hedef: uygulama herkesin açabileceği bir adreste çalışsın, kodu GitHub'a her gönderdiğinde testler koşsun, testler geçerse kendiliğinden güncellensin. **Ücretsiz** katmanlarla.

| Parça | Nerede | Neden |
|---|---|---|
| Veritabanı (PostgreSQL) | **Neon** | Ücretsiz katman süre sınırı olmadan kalıyor. (Render'ın ücretsiz Postgres'i 30 gün sonra siliniyor.) |
| API (Docker) | **Render** web servisi | Depodaki `GorevTakip/Dockerfile`'ı kendisi derler |
| Arayüz (statik dosyalar) | **Render** statik site | `npm run build` çıktısını sunar |
| Test + derleme | **GitHub Actions** | `.github/workflows/ci.yml` |

Ücretsiz katmanın bilinen davranışları (bunlar hata değil):

- **API 15 dakika istek almazsa uyur**, sonraki ilk istek ~1 dakikada uyanır. İlk açılışta "Yükleniyor…" uzun sürebilir.
- Neon veritabanı 5 dakika boşta kalınca uykuya geçer; uyanması genellikle bir saniyeden kısa sürer (uygulama bağlantıyı otomatik tekrar dener).
- Render ücretsiz katmanında ayda 750 saat çalışma süresi vardır (tek servis için ay boyunca yeter).

---

## 0. Bir kez yapılacak hazırlıklar (yerelde)

### 0.1 Veritabanı migration'ı

Aşama 5'te veritabanı SQLite'tan PostgreSQL'e geçti ve eski SQLite migration'ları silindi. Aşama 5 dosyalarını zip'ten kendi klasörüne kopyaladıysan eski dosyalar yerinde kalmıştır; **önce onları sil** (yoksa yeni `Ilk` ile aynı adlı iki sınıf olur ve derleme hata verir). Sonra yenisini üret ve commit'le:

```bash
cd GorevTakip
rm -rf Migrations                          # Windows PowerShell: Remove-Item -Recurse -Force Migrations
dotnet tool install --global dotnet-ef     # daha önce kurduysan atla
dotnet ef migrations add Ilk               # veritabanı gerekmez; sadece dosya üretir
```

`GorevTakip/Migrations/` klasörü oluşur. **Commit'lemeyi unutma**; yoksa uygulama canlıda açılışta açıklayıcı bir hatayla durur ("Hiç migration bulunamadı").

### 0.2 Test projesi

`GorevTakip.Tests/GorevTakip.Tests.csproj` dosyasını oluşturup commit'le (4 komut: [`GorevTakip.Tests/README.md`](GorevTakip.Tests/README.md) → "İlk kurulum"). CI `dotnet test GorevTakip.Tests` komutunu bu dosyayla koşar.

### 0.3 Yerelde her şeyi dene

```bash
cp .env.example .env
docker compose up --build          # Docker Desktop açık olmalı
```

- Arayüz → http://localhost:8080 (kayıt ol, proje aç)
- API → http://localhost:5291/health

Başka bir terminalde gerçek API'ye duman testi: `bash scripts/smoke-test.sh` (Windows'ta Git Bash ya da WSL).

---

## 1. GitHub'a gönder

```bash
git add -A
git commit -m "Aşama 5: giriş (JWT), PostgreSQL, Docker, CI/CD"
git push -u origin <dal-adı>
```

Önce bir pull request aç; **Actions** sekmesinde üç iş görünür: API (derle + test), Arayüz (lint + test + build + e2e), Docker (imajlar + gerçek veritabanıyla duman testi). `main`'e girince dördüncüsü de çalışır: API imajını yükleme. Hepsi yeşilse `main`'e birleştir.

> `main` dalını korumak istersen: Settings → Branches → "Require status checks to pass" ve ilk üç işi seç.

---

## 2. Neon: veritabanını oluştur

1. https://neon.com adresinde GitHub ile giriş yap → **Create project**.
2. Bölge: **AWS Europe (Frankfurt)** (Render'ın Frankfurt bölgesine yakın olsun).
3. Proje oluşunca **Connect** penceresinde bağlantı adresini kopyala. **"Connection pooling" kutusunun işaretini kaldır** (doğrudan bağlantı adresi). Havuzlu adres (`-pooler` içeren sunucu adı) EF Core migration'larının kullandığı kilitlerle sorun çıkarabilir.
4. Adres şuna benzer: `postgresql://kullanici:parola@ep-xxxx.eu-central-1.aws.neon.tech/neondb?sslmode=require`. Bunu bir yere not et; **gizlidir**, GitHub'a yazma.

Uygulama bu `postgresql://` biçimini doğrudan kabul eder (Npgsql'in kendi biçimine çevirir).

---

## 3. Render: iki servisi kur

1. https://render.com → GitHub ile giriş yap, depoya erişim ver.
2. **New → Blueprint** → bu depoyu seç. Render `render.yaml`'ı okur ve iki servis önerir: `gorevtakip-api`, `gorevtakip-ui`.
3. Sorduğu değerleri gir:

   | Servis | Değişken | Değer |
   |---|---|---|
   | gorevtakip-api | `ConnectionStrings__Default` | Neon adresi (2. adım) |
   | gorevtakip-api | `Cors__AllowedOrigins__0` | şimdilik `https://gorevtakip-ui.onrender.com` yaz (4. adımda düzelteceğiz) |
   | gorevtakip-ui | `VITE_API_URL` | şimdilik `https://gorevtakip-api.onrender.com` yaz (4. adımda düzelteceğiz) |

   `Jwt__Key` otomatik üretilir; elle girmen gerekmez.
4. **Apply**. İlk derleme birkaç dakika sürer. API açılışta migration'ları Neon'a uygular (`Database__MigrateOnStartup=true`).

## 4. Gerçek adresleri bağla

Servis adları başkasında alınmışsa Render sonuna rastgele harfler ekler (`gorevtakip-ui-x7k2.onrender.com`). Panelde iki servisin **gerçek adreslerine** bak ve düzelt:

- `gorevtakip-api` → Environment → `Cors__AllowedOrigins__0` = **arayüzün** adresi. Sonunda `/` olmasın.
- `gorevtakip-ui` → Environment → `VITE_API_URL` = **API'nin** adresi. Sonunda `/` olmasın.

Arayüzde değişikliğin geçerli olması için **Manual Deploy → Clear build cache & deploy** yap: Vite adresi derleme sırasında koda gömer, çalışırken okumaz.

## 5. Doğrula

```bash
bash scripts/smoke-test.sh https://<api-adresin> https://<arayuz-adresin>
```

Betik kayıt, giriş, yetkisiz erişim, iki kullanıcının birbirinin verisini görememesi ve CORS'u gerçek canlı API'de dener. Sonra tarayıcıdan arayüzü aç, hesap oluştur.

---

## 6. Her push'ta ne olur?

1. GitHub Actions testleri koşar (API, arayüz, Docker + gerçek veritabanıyla duman testi).
2. Hepsi geçerse (`autoDeployTrigger: checksPass`) Render yeni sürümü derleyip yayına alır.
3. Bir test kırılırsa **canlıya hiçbir şey gitmez**.
4. `main`'e girişte API imajı ayrıca `ghcr.io/<kullanici>/<depo>/api` olarak GitHub Container Registry'ye yüklenir (Render bunu kullanmaz, kendi derlemesini yapar; imaj başka yerde çalıştırmak istersen hazır durur).

---

## Sorun giderme

| Belirti | Neden / çözüm |
|---|---|
| Tarayıcı konsolunda "CORS" hatası | `Cors__AllowedOrigins__0` arayüzün tam adresiyle birebir aynı olmalı (`https://`, sonunda `/` yok). Değiştirince API'nin yeniden başlaması gerekir (Render otomatik yapar). |
| Arayüz `localhost:5291`'e istek atıyor | `VITE_API_URL` derleme sırasında okunur; değiştirdikten sonra arayüzü "Clear build cache & deploy" ile yeniden derle. |
| API açılmıyor, günlükte "Hiç migration bulunamadı" | 0.1 adımındaki `Migrations/` klasörü commit'lenmemiş. |
| API açılmıyor, günlükte "Jwt:Key" hatası | `Jwt__Key` boş ya da 32 karakterden kısa. |
| `password authentication failed` / `SSL` hatası | Neon adresini bozulmadan kopyala; `sslmode=require` kalsın. Parolayı Neon panelinden sıfırlayabilirsin (yeni adresi Render'a gir). |
| Giriş ekranında sürekli "Çok fazla deneme" | Giriş/kayıt IP başına dakikada 10 istekle sınırlı (`RateLimiting:AuthPerMinute`). Bir dakika bekle. |
| `Jwt__Key` değiştirdim, herkes çıkış yaptı | Beklenen davranış: eski anahtarla imzalı token'lar artık geçersiz. |
| İlk açılış çok yavaş | Ücretsiz katmanda uyuyan API'nin uyanması (~1 dk). Sonraki istekler hızlıdır. |

## Güvenlik notları

- **Gizli değerler** (`Jwt__Key`, Neon parolası) yalnızca Render panelinde durur; depoda yoktur. `appsettings.Development.json`'daki anahtar yalnızca yerel geliştirme içindir ve canlıda kullanılmaz.
- Parolalar PBKDF2-SHA256 ile (600.000 yineleme, kullanıcıya özel tuz) özetlenir; düz metin saklanmaz.
- Token tarayıcının `localStorage`'ında durur. Bu, sayfaya zararlı betik sokulabilirse (XSS) token'ın çalınabileceği anlamına gelir; React metni otomatik kaçışladığı için bu uygulamada HTML olarak basılan kullanıcı verisi yoktur. Daha sıkı alternatif `HttpOnly` çerezdir (ayrı bir tasarım gerektirir).
- Token ömrü 24 saattir (`Jwt:ExpiresMinutes`). Süresi dolunca ya da sunucu reddedince arayüz giriş ekranına döner.
