# GorevTakip — Mini Trello API

Projeler ve projelere bağlı görevleri yönetmek için ASP.NET Core Minimal API.
Görevler üç durumdan birinde olur: `Todo`, `InProgress`, `Done`.

**Teknolojiler:** C#, ASP.NET Core Minimal API, Entity Framework Core, SQLite, FluentValidation, Swagger (Swashbuckle)

## Mimari

```
Endpoints/       HTTP katmanı: isteği alır, servise devreder, cevabı üretir
Services/        İş mantığı ve veritabanı erişimi (arayüz + uygulama)
Validators/      FluentValidation ile istek doğrulama kuralları
Infrastructure/  Global hata yakalayıcı, doğrulama filtresi
Dtos/            API'nin dış veri şekilleri
Models/          Veritabanı varlıkları
Data/            EF Core DbContext
```

- Doğrulama hataları `400` + alan bazlı hata listesi döner.
- Beklenmeyen hatalar `500` + standart `ProblemDetails` döner, ayrıntı log'a yazılır.

## Çalıştırma

```bash
dotnet tool install --global dotnet-ef   # bir kez
dotnet ef database update                # migration'lardan gorevtakip.db'yi oluşturur
dotnet run                               # http://localhost:5291
```

API'yi tarayıcıdan denemek için http://localhost:5291/swagger adresini aç.

## Endpoint'ler

| Metot | Yol | Açıklama |
|---|---|---|
| GET | `/projects` | Projeleri listele |
| GET | `/projects/{id}` | Projeyi görevleriyle getir |
| POST | `/projects` | Proje oluştur |
| PUT | `/projects/{id}` | Projeyi güncelle |
| DELETE | `/projects/{id}` | Projeyi (ve görevlerini) sil |
| GET | `/projects/{projectId}/tasks?status=Done` | Projenin görevleri (durum filtresi opsiyonel) |
| POST | `/projects/{projectId}/tasks` | Projeye görev ekle |
| GET | `/tasks/{id}` | Görevi getir |
| PUT | `/tasks/{id}` | Görevi güncelle |
| PATCH | `/tasks/{id}/status` | Sadece durumu değiştir |
| DELETE | `/tasks/{id}` | Görevi sil |
