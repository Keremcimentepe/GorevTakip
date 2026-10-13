using GorevTakip.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GorevTakip.Tests.Support;

/// <summary>
/// Gerçek API'yi (Program.cs'teki tüm pipeline: CORS, kimlik doğrulama, doğrulama filtresi, hata
/// yakalayıcı...) bellekte ayağa kaldırır. Her fabrika kendi bellek içi SQLite veritabanını kullanır;
/// böylece testler birbirinden ve geliştirme veritabanından tamamen yalıtılır.
/// (Üretimde PostgreSQL kullanılır; testler hız için SQLite'a geçer. Program.cs'teki
/// "Database:Provider" ayarı bunun içindir.)
/// </summary>
public sealed class GorevTakipFactory : WebApplicationFactory<Program>
{
    // Testlerde kullanılan JWT ayarları. Testler kendi token'larını da üretebilsin diye açıkta.
    public const string JwtKey = "test-icin-uretilmis-gizli-anahtar-0123456789-abcdef";
    public const string JwtIssuer = "GorevTakip";
    public const string JwtAudience = "GorevTakip";

    private readonly string _databaseName = $"gorevtakip-test-{Guid.NewGuid():N}";
    private readonly SqliteConnection _keepAlive;
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly IDictionary<string, string?>? _extraSettings;

    /// <param name="configureServices">
    /// İsteğe bağlı: testte bir servisi sahtesiyle değiştirmek için (ör. hata fırlatan servis).
    /// </param>
    /// <param name="extraSettings">
    /// İsteğe bağlı: bu testte ayrıca verilecek ayarlar (ör. "Cors:AllowedOrigins:0"); varsayılanları ezer.
    /// </param>
    public GorevTakipFactory(
        Action<IServiceCollection>? configureServices = null,
        IDictionary<string, string?>? extraSettings = null)
    {
        _configureServices = configureServices;
        _extraSettings = extraSettings;

        // Paylaşımlı bellek veritabanı, kendisine bağlı en az bir bağlantı açık olduğu sürece yaşar.
        // EF her işlemden sonra kendi bağlantısını kapatır; bu bağlantı veritabanını ayakta tutar.
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();
    }

    private string ConnectionString =>
        $"Data Source={_databaseName};Mode=Memory;Cache=Shared;Foreign Keys=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Ayarlar appsettings dosyalarının ÜSTÜNE yazılır (sonradan eklenen kaynak kazanır).
        var settings = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:Default"] = ConnectionString,
            ["Jwt:Key"] = JwtKey,
            ["Jwt:Issuer"] = JwtIssuer,
            ["Jwt:Audience"] = JwtAudience,
            ["PasswordHashing:Iterations"] = "1000",  // testlerde şifre hash'i hızlı olsun
            ["RateLimiting:AuthPerMinute"] = "1000",  // testler art arda çok hesap açar
        };

        if (_extraSettings is not null)
        {
            foreach (var (key, value) in _extraSettings)
                settings[key] = value;
        }

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));

        if (_configureServices is not null)
            builder.ConfigureTestServices(_configureServices);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Emniyet kemeri: ayar uygulanmadıysa gerçek veritabanına dokunmadan dur.
        if (db.Database.GetConnectionString()?.Contains(_databaseName) != true)
            throw new InvalidOperationException(
                "Test veritabanı ayarı uygulanmadı; gerçek veritabanına dokunmamak için test durduruldu.");

        db.Database.EnsureCreated(); // tabloları modelden oluşturur

        return host;
    }

    /// <summary>
    /// İlk istekte kendiliğinden hesap açıp giriş yapan bir istemci verir; sonraki bütün isteklere
    /// token'ı ekler. Böylece çoğu test "giriş yapmış kullanıcı" gibi davranabilir.
    /// İki farklı kullanıcı gerekiyorsa farklı e-postalarla iki istemci oluşturun.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string email = "kullanici@ornek.com", string password = "Sifre1234!") =>
        CreateDefaultClient(new AutoLoginHandler(email, password));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _keepAlive.Dispose();
    }
}
