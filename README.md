# GorevTakip — Mini Trello

Projeleri ve projelere bağlı görevleri yönetmek için bir web uygulaması.
Görevler üç kolonlu bir panoda durur: **Yapılacak → Devam ediyor → Tamamlandı**.

| Klasör | İçerik | Teknolojiler |
|---|---|---|
| [`GorevTakip/`](GorevTakip) | REST API | C#, ASP.NET Core Minimal API, EF Core, SQLite, FluentValidation, Swagger |
| [`gorevtakip-ui/`](gorevtakip-ui) | Arayüz | React 19, TypeScript, Vite |

Her klasörün kendi README'sinde mimari ve ayrıntılar var.

## Çalıştırma

İki terminal gerekir:

```bash
# 1. terminal: API → http://localhost:5291 (Swagger: /swagger)
cd GorevTakip
dotnet tool install --global dotnet-ef   # bir kez
dotnet ef database update                # bir kez, veritabanını oluşturur
dotnet run

# 2. terminal: arayüz → http://localhost:5173
cd gorevtakip-ui
npm install                              # bir kez
cp .env.example .env                     # bir kez
npm run dev
```

## Yol haritası

- [x] Aşama 1: CRUD API, SQLite, Swagger
- [x] Aşama 2: Global hata yönetimi, FluentValidation, servis katmanı, CORS
- [x] Aşama 3: React arayüzü
- [ ] Aşama 4: Testler (API + Vitest/React Testing Library + Playwright)
- [ ] Aşama 5: Kullanıcı girişi (JWT), PostgreSQL, Docker, CI/CD, deploy
