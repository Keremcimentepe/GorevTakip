# GorevTakip UI — Mini Trello Arayüzü

Aynı depodaki [GorevTakip API](../GorevTakip)'sini kullanan React arayüzü.
Hesap oluştur ya da giriş yap; projelerini soldan seç, görevleri üç kolonlu bir panoda yönet: **Yapılacak → Devam ediyor → Tamamlandı**.
Kartları sürükleyip bırakarak ya da ← → butonlarıyla taşıyabilirsin. Her kullanıcı yalnızca kendi projelerini görür.

**Teknolojiler:** React 19, TypeScript, Vite, saf CSS (harici UI kütüphanesi yok). Testler: Vitest, React Testing Library, Playwright

## Çalıştırma

Önce API'nin (ve PostgreSQL'in) çalışıyor olması gerekir: depo kökündeki README'deki "Seçenek B" adımları (`docker compose up -d db`, ardından `../GorevTakip` klasöründe `dotnet run`, varsayılan adres `http://localhost:5291`).

```bash
npm install
cp .env.example .env     # VITE_API_URL API adresinden farklıysa düzenle
npm run dev              # http://localhost:5173
```

API'deki CORS ayarı yerelde yalnızca `http://localhost:5173` ve `http://localhost:3000` adreslerine izin verir (canlıda `Cors__AllowedOrigins__0` ile ayarlanır).

**Docker ile:** `docker build --build-arg VITE_API_URL=<API adresi> -t gorevtakip-ui .` — Vite API adresini derleme sırasında koda gömer, bu yüzden adres imajı derlerken verilir (verilmezse derleme durur). nginx ile 8080 portunda sunar. Hepsini birlikte ayağa kaldırmak için depo kökündeki `docker-compose.yml`.

## Klasör yapısı

```
src/
├── types.ts            API'deki DTO'ların TypeScript karşılıkları, kolon tanımları
├── api/                HTTP katmanı: adres, JSON, token ve hata mesajı işleri tek yerde
│   ├── client.ts       istekleri gönderir; token'ı ekler; 401'de oturumu kapatır
│   ├── session.ts      oturum (token + e-posta + bitiş) localStorage'da
│   ├── auth.ts         login / register
│   ├── projects.ts
│   └── tasks.ts
├── hooks/              Durum ve iş mantığı (bileşenlerden ayrı)
│   ├── useAuth.ts      giriş durumu, giriş/kayıt/çıkış, "oturum süresi doldu" bilgisi
│   ├── useProjects.ts  proje listesi
│   └── useBoard.ts     seçili projenin panosu, kart taşıma/ekleme/silme
├── components/         Sadece görünüm
│   ├── AuthForm.tsx    giriş / kayıt formu
│   ├── ProjectList.tsx
│   ├── Board.tsx
│   ├── Column.tsx
│   ├── TaskCard.tsx
│   └── AddForm.tsx
├── test/               Testlerin ortak malzemesi
│   ├── setup.ts        jest-dom eşleştiricileri + her testten sonra temizlik
│   └── factories.ts    makeTask / makeProject / makeProjectDetail
├── App.tsx             kapı (oturum yoksa AuthForm) + çalışma alanı (proje listesi, pano)
└── main.tsx            giriş noktası
                        (*.test.ts(x) dosyaları, test ettikleri dosyanın yanında durur)
e2e/                    Playwright testleri (gerçek tarayıcı)
├── fake-api.ts         bellekte çalışan sahte backend (giriş, token kontrolü, kullanıcıya özel veri)
├── app-page.ts         sayfa nesnesi: "giriş yap", "proje aç", "kartı taşı" gibi kullanıcı eylemleri
├── fixtures.ts         her teste temiz bir sahte API + AppPage verir; varsayılan olarak girişli başlatır
└── *.spec.ts           giriş/oturum, projeler, pano, hata durumları, dar ekran
```

## Testler

```bash
npm test                     # Vitest: api/, hook'lar, bileşenler (saniyeler sürer, tarayıcı açmaz)
npm run test:watch           # dosyayı kaydettikçe yeniden koşar
npx playwright install chromium   # bir kez: Playwright'ın tarayıcısını indirir
npm run test:e2e             # Playwright: gerçek Chromium'da uçtan uca (dev sunucusunu kendisi açar)
npx playwright show-report   # son koşunun HTML raporu
```

İlk kurulumda test araçları da kurulmalı (`package.json`'a ekler):

```bash
npm install -D vitest jsdom @testing-library/react @testing-library/jest-dom @testing-library/user-event @playwright/test
```

**Hangi katman neyi sınar**

- **`api/` (Vitest):** `fetch` sahtelenir; adres birleştirme, JSON gövdesi, 204 cevabı, `400` alan hatalarının birleştirilmesi, `500` başlığı, ağ hatası mesajı; **token ekleme, `401`'de oturumu kapatma, `429` mesajı, oturum saklama (süre dolumu, bozuk kayıt, localStorage'ın çalışmaması, başka sekmeden haber)**.
- **Hook'lar (Vitest):** `useBoard` iyimser güncellemeyi (istek bitmeden ekran değişir) ve hata olunca geri almayı, `useProjects` ekleme/silme/hata akışını, `useAuth` giriş/kayıt/çıkış ve "oturum süresi doldu" bilgisini sınar. `api/` modülleri sahtelenir.
- **Bileşenler (Vitest + Testing Library):** Kullanıcı gibi davranılır (etikete göre bul, yaz, tıkla); iç state'e ya da CSS sınıfına bakılmaz. `Board` testi, `Board + useBoard + Column + TaskCard + AddForm` birlikte çalışırken yalnızca ağ katmanını sahteler.
- **Uçtan uca (Playwright):** Gerçek tarayıcıda, `page.route` ile API sahtelenerek: giriş/kayıt/çıkış, sayfa yenilenince oturumun sürmesi, sunucu token'ı reddedince giriş ekranına dönüş, iki hesabın birbirinin verisini görmemesi, başka sekmede çıkış/hesap değişimi, proje/görev ekleme, ← → ile taşıma, gerçek sürükle-bırak, silme (onaylı), projeler arası geçiş, `400`/`404`/`500`/sunucu kapalı durumları, dar ekran düzeni.

**Bilinen sınırlar**

- jsdom sürükle-bırak olaylarını (`DragEvent`) tam desteklemez; bu yüzden bileşen testleri yalnızca `dataTransfer` sözleşmesini sınar, gerçek sürükle-bırak Playwright'ta denenir.
- `e2e/fake-api.ts` gerçek API'nin kopyası değildir, API değişirse elle güncellenmelidir. Gerçek API ile gerçek akış depo kökündeki `scripts/smoke-test.sh` ile (CI'da Docker üzerinde) ayrıca sınanır; gerçek tarayıcı + gerçek API birlikte sınanmaz.
- Testlerde varsayılan olarak giriş yapılmış başlanır (`fixtures.ts` oturumu sayfa açılmadan localStorage'a yazar); giriş ekranını sınayan dosyalar `test.use({ signedIn: false })` der.
- `npm run build` (`tsc -b`) `src/` içindeki test dosyalarını da tip denetiminden geçirir; testte tip hatası varsa derleme de kırılır.

## Önemli tasarım kararları

- **Katmanlı yapı:** Bileşenler `fetch` bilmez; `hooks` → `api` → sunucu. Backend adresi ya da hata biçimi değişirse tek dosya değişir.
- **İyimser güncelleme:** Kart taşınınca ekran hemen güncellenir, sunucu hata verirse kart eski yerine döner ve hata mesajı gösterilir.
- **Hata mesajları:** Backend'in `400` doğrulama cevabındaki alan hataları ve `500` cevabındaki başlık kullanıcıya okunur biçimde gösterilir; sunucuya ulaşılamazsa ayrı bir mesaj çıkar.
- **`key` ile durum sıfırlama:** Proje değişince `Board`, kullanıcı değişince çalışma alanı yeniden kurulur; önceki projenin/kullanıcının durumu sızmaz.
- **Oturum tek yerde:** `session.ts` token'ı localStorage'da tutar (süresi dolmuşsa "yok" sayar, bozuk kaydı yok sayar, depolama çalışmıyorsa bellekte tutar). React bunu `useSyncExternalStore` ile okur; böylece başka sekmede çıkış/giriş yapılınca bu sekme de güncellenir.
- **401 = oturum bitti:** `client.ts` token gönderdiği bir istek `401` alırsa oturumu siler ve giriş ekranına "Oturumun süresi doldu" bilgisiyle döner. Yanlış şifreli giriş denemesi (`auth: false`) buna girmez. İstek yoldayken kullanıcı yeniden giriş yaptıysa yeni oturum silinmez.
- **Doğrulama kuralı tek yerde:** E-posta biçimi/şifre uzunluğu yalnızca API'de denetlenir; mesajı formda gösterilir (`noValidate`: tarayıcının kendi, başka dildeki uyarısı çıkmasın).
- **Token `localStorage`'da:** Sayfaya zararlı betik sokulabilirse (XSS) çalınabilir. React metni otomatik kaçışladığı ve uygulamada HTML olarak basılan kullanıcı verisi olmadığı için kabul edildi; daha sıkı alternatif `HttpOnly` çerezdir.
