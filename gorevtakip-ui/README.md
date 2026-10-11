# GorevTakip UI — Mini Trello Arayüzü

Aynı depodaki [GorevTakip API](../GorevTakip)'sini kullanan React arayüzü.
Projeleri soldan seç, görevleri üç kolonlu bir panoda yönet: **Yapılacak → Devam ediyor → Tamamlandı**.
Kartları sürükleyip bırakarak ya da ← → butonlarıyla taşıyabilirsin.

**Teknolojiler:** React 19, TypeScript, Vite, saf CSS (harici UI kütüphanesi yok)

## Çalıştırma

Önce API'nin çalışıyor olması gerekir (`../GorevTakip` klasöründe `dotnet run`, varsayılan adres `http://localhost:5291`).

```bash
npm install
cp .env.example .env     # VITE_API_URL API adresinden farklıysa düzenle
npm run dev              # http://localhost:5173
```

API'deki CORS ayarı yalnızca `http://localhost:5173` ve `http://localhost:3000` adreslerine izin verir.

## Klasör yapısı

```
src/
├── types.ts            API'deki DTO'ların TypeScript karşılıkları, kolon tanımları
├── api/                HTTP katmanı: adres, JSON ve hata mesajı işleri tek yerde
│   ├── client.ts
│   ├── projects.ts
│   └── tasks.ts
├── hooks/              Durum ve iş mantığı (bileşenlerden ayrı)
│   ├── useProjects.ts  proje listesi
│   └── useBoard.ts     seçili projenin panosu, kart taşıma/ekleme/silme
├── components/         Sadece görünüm
│   ├── ProjectList.tsx
│   ├── Board.tsx
│   ├── Column.tsx
│   ├── TaskCard.tsx
│   └── AddForm.tsx
├── App.tsx             yerleşim ve seçili proje
└── main.tsx            giriş noktası
```

## Önemli tasarım kararları

- **Katmanlı yapı:** Bileşenler `fetch` bilmez; `hooks` → `api` → sunucu. Backend adresi ya da hata biçimi değişirse tek dosya değişir.
- **İyimser güncelleme:** Kart taşınınca ekran hemen güncellenir, sunucu hata verirse kart eski yerine döner ve hata mesajı gösterilir.
- **Hata mesajları:** Backend'in `400` doğrulama cevabındaki alan hataları ve `500` cevabındaki başlık kullanıcıya okunur biçimde gösterilir; sunucuya ulaşılamazsa ayrı bir mesaj çıkar.
- **`key` ile durum sıfırlama:** Proje değişince `Board` yeniden kurulur, önceki projenin durumu sızmaz.
