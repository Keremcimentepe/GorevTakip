# GorevTakip.Tests — API testleri

[`GorevTakip`](../GorevTakip) API'sinin xUnit testleri. Testlerde veritabanı olarak **gerçek SQLite** (bellek içi) kullanılır, EF Core'un `InMemory` sağlayıcısı değil: yabancı anahtar / cascade silme ve `enum`'ın metin olarak saklanması üretimdekiyle aynı çalışsın diye. Üretim PostgreSQL'dir; testlerin Docker'a ya da internete ihtiyacı yoktur.

## İlk kurulum (bir kez)

Bu klasörde `.csproj` yok; paket sürümlerini kendi makinen (`dotnet` CLI) seçsin diye. Depo kökünden:

```bash
dotnet new xunit -o GorevTakip.Tests                                   # .csproj'u üretir (klasör zaten dolu, sorun değil)
rm GorevTakip.Tests/UnitTest1.cs                                       # şablonun örnek testi; Windows PowerShell: Remove-Item
dotnet add GorevTakip.Tests reference GorevTakip/GorevTakip.csproj
dotnet add GorevTakip.Tests package Microsoft.AspNetCore.Mvc.Testing   # WebApplicationFactory buradan gelir
```

Oluşan `GorevTakip.Tests/GorevTakip.Tests.csproj` dosyasını **git'e ekle** (CI onu kullanır). `Microsoft.AspNetCore.Mvc.Testing` sürümü API'nin .NET sürümüyle aynı ana sürümde olmalı (`10.*`).

İstersen Visual Studio'da Test Gezgini'nin görmesi için bir çözüm dosyası oluştur:

```bash
dotnet new sln -n GorevTakip
dotnet sln add GorevTakip/GorevTakip.csproj GorevTakip.Tests/GorevTakip.Tests.csproj
```

## Çalıştırma

```bash
dotnet test GorevTakip.Tests
dotnet test GorevTakip.Tests --filter "FullyQualifiedName~Validator"   # sadece doğrulayıcılar
dotnet test GorevTakip.Tests --filter "FullyQualifiedName~Ownership"  # sadece "kullanıcılar birbirinin verisini göremez"
```

## Klasör yapısı

```
Validators/       FluentValidation kuralları: boş/boşluk, sınır değerler (100/101, 200/201, ...), enum, tam Türkçe mesajlar
Dtos/             Model → DTO eşlemeleri
Services/         İş mantığı: kırpma, sıralama, durum filtresi, cascade silme, "bulunamadı" dönüşleri,
                  kayıt/giriş, şifre özeti (hash), token üretimi, başkasının verisine erişememe
Infrastructure/   postgres:// adresini Npgsql bağlantı dizesine çeviren sınıf, token'dan kullanıcı kimliği okuma
Api/              Entegrasyon: uygulama bellekte ayağa kalkar, gerçek HTTP istekleri atılır
                  (kayıt/giriş, 401 matrisi, bozuk/süresi dolmuş token, hız sınırı, kullanıcı yalıtımı, CORS)
Support/          Ortak yardımcılar (fabrika, test veritabanı, istek yardımcıları, otomatik giriş yapan istemci)
```

## Önemli tasarım kararları

- **Test veritabanı geliştirme veritabanından ayrıdır.** `GorevTakipFactory`, ayarları `ConfigureAppConfiguration` + bellek içi koleksiyonla verir (`appsettings`'in üstüne yazar): `Database:Provider=Sqlite`, paylaşımlı bellek içi bağlantı dizesi, test JWT anahtarı. Uygulama ayağa kalkarken gerçekten bellek içi veritabanına bağlandığını doğrular; bağlanmamışsa gerçek bir dosya/veritabanına yazmak yerine testi hata ile durdurur.
- **Her test kendi veritabanını alır:** xUnit her test için sınıfı yeniden oluşturur; kurucu yeni bir fabrika (adında `Guid` olan paylaşımlı bellek içi SQLite) ya da yeni bir `TestDb` kurar, `Dispose` da yok eder. Testler birbirinin verisini görmez, sıraya bağımlı değildir ve paralel koşabilir.
- **Giriş yapmış istemci:** `factory.CreateAuthenticatedClient("ayse@ornek.com")` o e-postayla kayıt olur, token'ı alır ve her isteğe `Authorization: Bearer ...` ekler (`AutoLoginHandler`). İki farklı e-postayla iki istemci açıp yalıtım test edilir.
- **Başkasının kaydı 403 değil 404 döner:** "bu kayıt var ama senin değil" bilgisini bile sızdırmamak için. `OwnershipApiTests` bunu her uç nokta için ve "hiçbir şey değişmedi" kontrolüyle birlikte kilitler.
- **Sahte token'lar:** `TestTokens.Create(...)` ile süresi dolmuş, yanlış anahtarla imzalı, yanlış `aud`'lu, imzasız ve içeriği değiştirilmiş token'lar üretilir; hepsinin `401` döndüğü denetlenir.
- **Şifre ve giriş:** testlerde PBKDF2 yineleme sayısı 1000'e indirilir (üretim 600.000) ki testler hızlı olsun. Yanlış şifre ile var olmayan e-posta **aynı** hata başlığını döner (hangi e-postaların kayıtlı olduğu sızmaz).
- **Doğrulama, 404 kontrolünden önce çalışır:** geçersiz gövdeyle var olmayan bir projeye görev eklenirse `404` değil `400` döner. Test bu sırayı bilerek kilitler.
- **Hata testi sahte servisle yapılır:** `ConfigureTestServices` ile istisna fırlatan bir `ITaskService` takılır; cevabın `500` olduğu, başlığın "Beklenmeyen bir hata oluştu." olduğu ve istisna ayrıntısının dışarı sızmadığı denetlenir.
- **Cevap JSON olarak ayrıştırılır, metin olarak aranmaz:** Türkçe karakterler JSON'da `\uXXXX` olarak kaçışlanabildiği için `Contains("başlığı")` gibi aramalar yanıltıcı olurdu.
- **Hız sınırı ayrı fabrikada test edilir:** diğer testler `RateLimiting:AuthPerMinute=1000` ile koşar; `AuthRateLimitTests` ise limiti 3'e indirip 4. denemenin `429` aldığını kontrol eder.
