# GörevTakip — Mini Trello

Kullanıcıların kendi projelerini ve projelere bağlı görevlerini yönettiği bir web uygulaması.
Görevler üç kolonlu bir panoda durur: **Yapılacak → Devam ediyor → Tamamlandı**. Her kullanıcı yalnızca kendi verisini görür.

| Klasör | İçerik | Teknolojiler |
|---|---|---|
| [`GorevTakip/`](GorevTakip) | REST API | C#, ASP.NET Core Minimal API, EF Core, PostgreSQL, JWT, FluentValidation, Swagger |
| [`GorevTakip.Tests/`](GorevTakip.Tests) | API testleri | xUnit, WebApplicationFactory, FluentValidation.TestHelper |
| [`gorevtakip-ui/`](gorevtakip-ui) | Arayüz + arayüz testleri | React 19, TypeScript, Vite, Vitest, React Testing Library, Playwright |
| [`scripts/`](scripts) | Gerçek API'ye duman testi | bash + curl |
| `Dockerfile`'lar, `docker-compose.yml`, `.github/workflows/`, `render.yaml` | Paketleme, CI/CD, yayın | Docker, GitHub Actions, Render, Neon |
| `docker-compose.prod.yml`, `deploy/` | Kendi sunucuna (Oracle Cloud VDS) yayın: HTTPS, kurulum/güncelleme/yedek betikleri | Docker Compose, Caddy |

Her klasörün kendi README'sinde mimari ve ayrıntılar var. **Yayına alma adımları: [`DEPLOY.md`](DEPLOY.md) (Render + Neon) ya da kendi sunucun için [`DEPLOY-ORACLE.md`](DEPLOY-ORACLE.md).**

```
Tarayıcı ──(JWT)──► React (statik site)
                        │  Authorization: Bearer ...
                        ▼
                    ASP.NET Core API (Docker) ──► PostgreSQL (Neon)
```

## Çalıştırma

### Seçenek A — Her şey Docker'da (en kolay)

```bash
cp .env.example .env           # bir kez
docker compose up --build      # arayüz → http://localhost:8080, API → http://localhost:5291
```

> İlk kez çalıştırmadan önce migration üretilmiş olmalı: `cd GorevTakip && dotnet ef migrations add Ilk` (ayrıntı: [`DEPLOY.md`](DEPLOY.md) → 0.1).

### Seçenek B — Geliştirme (kod değişince anında yenilenir)

```bash
# 1. veritabanı (yalnızca PostgreSQL konteyneri)
cp .env.example .env
docker compose up -d db

# 2. API → http://localhost:5291 (Swagger: /swagger)
cd GorevTakip
dotnet tool install --global dotnet-ef   # bir kez
dotnet ef migrations add Ilk             # bir kez (Migrations/ klasörü yoksa)
dotnet ef database update
dotnet run

# 3. arayüz → http://localhost:5173
cd gorevtakip-ui
npm install                              # bir kez
cp .env.example .env                     # bir kez
npm run dev
```

Arayüzü açınca önce bir hesap oluştur (e-posta + en az 8 karakterli şifre).

## Testler

Testler uygulamayı çalıştırmaktan bağımsızdır: API testleri kendi bellek içi veritabanını, tarayıcı testleri sahte bir backend'i kullanır; PostgreSQL ya da Docker gerekmez.

```bash
# API testleri (ilk kurulum için GorevTakip.Tests/README.md)
dotnet test GorevTakip.Tests

# Arayüz: bileşen/hook testleri (hızlı, tarayıcı yok)
cd gorevtakip-ui
npm test

# Arayüz: tarayıcıda uçtan uca testler (ilk seferde: npx playwright install chromium)
npm run test:e2e

# Gerçek API + gerçek PostgreSQL'e duman testi (docker compose up ile ayağa kalktıktan sonra)
bash scripts/smoke-test.sh
```

| Katman | Ne sınar | Nasıl |
|---|---|---|
| Doğrulayıcılar | Alan kuralları, sınır değerler, Türkçe hata mesajları | Doğrudan `TestValidate` |
| Servisler | İş mantığı, sıralama, kırpma, cascade silme, kayıt/giriş, şifre özeti, token | Gerçek SQLite (bellek içi) |
| API (entegrasyon) | Durum kodları, JSON şekli, 400/401/404/409/429/500, CORS, **kullanıcıların birbirinin verisini görememesi** | `WebApplicationFactory<Program>` ile uygulama bellekte ayağa kalkar |
| UI birim/bileşen | `api/` katmanı, oturum saklama, hook'lar (iyimser güncelleme + geri alma, giriş), bileşenler | Vitest + React Testing Library (jsdom) |
| UI uçtan uca | Gerçek tarayıcıda giriş/kayıt/çıkış, süresi dolan oturum, iki hesap, proje/görev akışı, sürükle-bırak, hata durumları, dar ekran | Playwright + sahte backend (`page.route`) |
| Duman testi | Gerçek API + PostgreSQL: kayıt, giriş, 401, kullanıcı yalıtımı, CORS | `scripts/smoke-test.sh` (CI'da `docker compose` ile) |

**Bilinen sınır:** Tarayıcı testlerindeki sahte backend gerçek API'nin kopyası değildir; API'deki mesajlar ya da uç noktalar değişirse `e2e/fake-api.ts` elle güncellenmelidir. Gerçek API ile gerçek akış CI'daki duman testiyle (`scripts/smoke-test.sh`) ayrıca sınanır, ama gerçek tarayıcı + gerçek API birlikte sınanmaz.

## CI/CD

`.github/workflows/ci.yml` her push ve pull request'te çalışır:

1. **API:** derle, xUnit testleri, "modelde migration'sız değişiklik var mı?" denetimi
2. **Arayüz:** lint, Vitest, build, Playwright
3. **Docker:** üç imajı derle, `docker compose` ile gerçek PostgreSQL'e karşı duman testi
4. **main'e girişte:** API imajını `ghcr.io`'ya yükle; Render, kontroller yeşilse canlıya alır

## Yol haritası

- [x] Aşama 1: CRUD API, SQLite, Swagger
- [x] Aşama 2: Global hata yönetimi, FluentValidation, servis katmanı, CORS
- [x] Aşama 3: React arayüzü
- [x] Aşama 4: Testler (API + Vitest/React Testing Library + Playwright)
- [x] Aşama 5: Kullanıcı girişi (JWT), kullanıcıya özel veri, PostgreSQL, Docker, CI/CD, yayın
