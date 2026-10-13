# GorevTakip — Mini Trello API

Projeler ve projelere bağlı görevler için ASP.NET Core Minimal API. Kullanıcılar kayıt olup giriş yapar (JWT);
her kullanıcı yalnızca **kendi** projelerini ve görevlerini görür.
Görevler üç durumdan birinde olur: `Todo`, `InProgress`, `Done`.

**Teknolojiler:** C#, ASP.NET Core Minimal API, Entity Framework Core, PostgreSQL (Npgsql), JWT (JwtBearer), PBKDF2 şifre özeti, FluentValidation, Swagger (Swashbuckle)

## Mimari

```
Endpoints/       HTTP katmanı: isteği alır, servise devreder, cevabı üretir (auth / projects / tasks)
Services/        İş mantığı ve veritabanı erişimi (arayüz + uygulama); kayıt/giriş, şifre özeti, token üretimi
Validators/      FluentValidation ile istek doğrulama kuralları
Infrastructure/  Global hata yakalayıcı, doğrulama filtresi, ayar sınıfları, postgres:// adres çevirici
Dtos/            API'nin dış veri şekilleri
Models/          Veritabanı varlıkları (User → Project → TaskItem)
Data/            EF Core DbContext
```

- Doğrulama hataları `400` + alan bazlı hata listesi döner.
- Beklenmeyen hatalar `500` + standart `ProblemDetails` döner, ayrıntı log'a yazılır.
- Token yok/geçersiz/süresi dolmuş → `401`. Başkasının kaydı → `404` (varlığını bile belli etmemek için; `403` değil).
- Giriş/kayıt IP başına dakikada en fazla `RateLimiting:AuthPerMinute` (10) istek alır, aşılırsa `429`.

## Kimlik doğrulama

1. `POST /auth/register` ya da `POST /auth/login` → `{ "token": "...", "expiresAt": "...", "email": "..." }`
2. Sonraki isteklerde `Authorization: Bearer <token>` başlığı.

- Şifre **PBKDF2-SHA256** ile (600.000 yineleme, kullanıcıya özel tuz) özetlenir; sabit zamanlı karşılaştırılır.
- Token **HS256** ile imzalanır; `sub` (kullanıcı kimliği) ve `email` taşır. Yalnızca HS256, doğru `iss`/`aud`, süresi dolmamış token kabul edilir.
- Yanlış şifre ile kayıtsız e-posta **aynı** cevabı alır (hesap varlığı sızmaz); kayıtsız e-postada da şifre özeti hesaplanır (zamanlama farkı oluşmasın).
- E-posta küçük harfe çevrilip kırpılarak saklanır; benzersizdir.

## Ayarlar

Ayarlar `appsettings*.json` dosyalarından ya da ortam değişkenlerinden gelir (`:` yerine `__`).

| Ayar | Ortam değişkeni | Açıklama |
|---|---|---|
| `ConnectionStrings:Default` | `ConnectionStrings__Default` | PostgreSQL. `Host=...;Database=...` ya da `postgresql://...` biçimi |
| `Jwt:Key` | `Jwt__Key` | Token imza anahtarı, **en az 32 karakter** (yoksa uygulama açılmaz) |
| `Jwt:ExpiresMinutes` | `Jwt__ExpiresMinutes` | Token ömrü, varsayılan 1440 (24 saat) |
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins__0`, `__1`... | Arayüzün adresi; boşsa yerel geliştirme adresleri |
| `Database:MigrateOnStartup` | `Database__MigrateOnStartup` | `true` ise açılışta migration'ları uygular (Docker/canlı) |
| `RateLimiting:AuthPerMinute` | `RateLimiting__AuthPerMinute` | Giriş/kayıt hız sınırı, varsayılan 10 |
| `PasswordHashing:Iterations` | `PasswordHashing__Iterations` | PBKDF2 yineleme sayısı (testlerde düşürülür) |
| `Database:Provider` | `Database__Provider` | `Sqlite` verilirse SQLite (yalnızca testler) |

`appsettings.Development.json` yerel geliştirme için bir bağlantı dizesi ve **yalnızca yerelde geçerli** bir `Jwt:Key` içerir; canlıda kullanılmaz.

## Çalıştırma

```bash
docker compose up -d db                  # depo kökünden: PostgreSQL (bir kez başlatman yeter)

dotnet tool install --global dotnet-ef   # bir kez
dotnet ef migrations add Ilk             # bir kez: Migrations/ klasörünü üretir, commit'le
dotnet ef database update                # şemayı veritabanına uygular
dotnet run                               # http://localhost:5291
```

http://localhost:5291/swagger (yalnızca Development ortamında) uç noktaları listeler; ancak Swagger'a "Authorize" (Bearer) düğmesi eklenmedi, bu yüzden korumalı uç noktalar oradan denenemez. Elle denemek için `curl` ya da Postman/Bruno kullan: önce `POST /auth/register` ile token al, sonra isteklere `Authorization: Bearer <token>` ekle. Hızlı bir tur için `scripts/smoke-test.sh`'a bak.

Model değiştirince (yeni alan vb.): `dotnet ef migrations add <Ad>`; CI, migration'sız model değişikliğini yakalar.

## Testler

Testler ayrı projede: [`../GorevTakip.Tests`](../GorevTakip.Tests) (kurulum ve ayrıntılar orada).

```bash
dotnet test ../GorevTakip.Tests
```

`Program.cs`'in sonundaki boş `public partial class Program { }` tanımı testler içindir: top-level statements'ın ürettiği `Program` sınıfı varsayılan olarak `internal` olduğu için, `WebApplicationFactory<Program>`'ın uygulamayı bellekte ayağa kaldırabilmesi için `public` yapılır.

## Endpoint'ler

| Metot | Yol | Giriş | Açıklama |
|---|---|---|---|
| GET | `/health` | — | Sağlık kontrolü (barındırma servisi için) |
| POST | `/auth/register` | — | Hesap oluştur, token döner (`409`: e-posta kayıtlı) |
| POST | `/auth/login` | — | Giriş yap, token döner (`401`: bilgiler hatalı) |
| GET | `/projects` | ✔ | Kendi projelerini listele |
| GET | `/projects/{id}` | ✔ | Projeyi görevleriyle getir |
| POST | `/projects` | ✔ | Proje oluştur |
| PUT | `/projects/{id}` | ✔ | Projeyi güncelle |
| DELETE | `/projects/{id}` | ✔ | Projeyi (ve görevlerini) sil |
| GET | `/projects/{projectId}/tasks?status=Done` | ✔ | Projenin görevleri (durum filtresi opsiyonel) |
| POST | `/projects/{projectId}/tasks` | ✔ | Projeye görev ekle |
| GET | `/tasks/{id}` | ✔ | Görevi getir |
| PUT | `/tasks/{id}` | ✔ | Görevi güncelle |
| PATCH | `/tasks/{id}/status` | ✔ | Sadece durumu değiştir |
| DELETE | `/tasks/{id}` | ✔ | Görevi sil |
