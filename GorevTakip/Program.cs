using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using GorevTakip.Data;
using GorevTakip.Endpoints;
using GorevTakip.Infrastructure;
using GorevTakip.Services;
using GorevTakip.Validators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Veritabanı ---
// Varsayılan sağlayıcı PostgreSQL. "Database:Provider" = "Sqlite" verilirse SQLite kullanılır;
// testler bu yolu kullanır (hızlı, bellek içi). Ayarlar KASITLI olarak bu lambda'nın İÇİNDE okunur:
// lambda, uygulama kurulduktan sonra çalışır; böylece testlerin ve ortam değişkenlerinin
// verdiği ayarlar da görünür.
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var configuration = builder.Configuration;
    var connectionString = configuration.GetConnectionString("Default");

    if (string.Equals(configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString ?? "Data Source=gorevtakip.db");
    }
    else
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Veritabanı bağlantısı ayarlanmamış. 'ConnectionStrings:Default' (ortam değişkeni: " +
                "ConnectionStrings__Default) ayarını verin; örnek: " +
                "Host=localhost;Port=5432;Database=gorevtakip;Username=gorevtakip;Password=...");

        options.UseNpgsql(
            PostgresConnectionString.Normalize(connectionString), // postgresql://... adresini de kabul eder
            npgsql => npgsql.EnableRetryOnFailure());              // veritabanı anlık kopsa tekrar dener
    }
});

// --- Kimlik doğrulama (JWT) ---
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(
        o => !string.IsNullOrWhiteSpace(o.Key) && o.Key.Length >= 32,
        "'Jwt:Key' ayarı en az 32 karakter olmalı (ortam değişkeni: Jwt__Key).")
    .ValidateOnStart(); // anahtar eksikse uygulama ilk istekte değil, açılırken hata verir

builder.Services.AddOptions<PasswordHashingOptions>()
    .Bind(builder.Configuration.GetSection(PasswordHashingOptions.SectionName));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Token doğrulama kuralları. Anahtar ayarlardan okunur; bu yüzden AddJwtBearer'ın içinde değil,
// ayarlar hazır olduktan sonra çalışan bu Configure bloğunda tanımlıyoruz.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;

        bearer.MapInboundClaims = false; // "sub" talebi olduğu gibi kalsın (ClaimTypes'a çevrilmesin)
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true, // süresi dolan token reddedilir
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }, // başka algoritmayla imzalı token kabul edilmez
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

// Brute force koruması: giriş/kayıt uç noktaları IP başına dakikada en fazla N istek alır.
// (Render gibi bir ters vekilin arkasında gerçek IP için ASPNETCORE_FORWARDEDHEADERS_ENABLED=true gerekir.)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
    {
        var permitLimit = httpContext.RequestServices
            .GetRequiredService<IConfiguration>()
            .GetValue("RateLimiting:AuthPerMinute", 10);

        return RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });
});

// --- Servisler (iş mantığı katmanı) ---
// Scoped: her HTTP isteği için bir tane oluşturulur (DbContext ile aynı ömür)
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IAuthService, AuthService>();
// Singleton: durumu (state) yok, tek örnek yeter
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<ITokenService, TokenService>();

// --- Doğrulama: Validators klasöründeki tüm validator sınıflarını otomatik kaydeder ---
builder.Services.AddValidatorsFromAssemblyContaining<CreateProjectRequestValidator>();

// --- Global hata yönetimi ---
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// --- Sağlık kontrolü: barındırma servisi "uygulama ayakta mı?" diye /health'e bakar ---
builder.Services.AddHealthChecks();

// --- CORS: React arayüzü farklı bir adresten API'ye istek atar ---
// İzinli adresler 'Cors:AllowedOrigins' ayarından gelir (ortam değişkeni: Cors__AllowedOrigins__0, __1...).
// Ayar yoksa yerel geliştirme adresleri (Vite ve Create React App) kullanılır.
builder.Services.AddCors(options =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(origins is { Length: > 0 } ? origins : new[] { "http://localhost:5173", "http://localhost:3000" })
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// Enum değerleri JSON'da sayı yerine okunur metin olsun ("Todo", "Done" gibi)
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// --- Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Hata yakalayıcı pipeline'ın EN BAŞINDA olmalı ki sonraki her şeydeki hatayı yakalasın
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Sıra önemli: CORS, kimlik doğrulamadan ÖNCE gelmeli. Yoksa 401 cevabı CORS başlığı taşımaz ve
// tarayıcı gerçek hatayı ("401") değil "CORS hatası" gösterir.
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication(); // "Bu token kimin?"
app.UseAuthorization();  // "Bu uç noktaya girebilir mi?"

app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapProjectEndpoints();
app.MapTaskEndpoints();

// --- Veritabanı şemasını açılışta güncelle ---
// Docker/canlı ortamda `Database__MigrateOnStartup=true` verilir: container, elle `dotnet ef database
// update` çalıştırmadan ayağa kalkabilir. Varsayılan KAPALI: `dotnet ef migrations add` komutu da
// Program.cs'i çalıştırır; açık olsaydı henüz migration yokken kendi kendini engellerdi.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!db.Database.GetMigrations().Any())
        throw new InvalidOperationException(
            "Hiç migration bulunamadı. Proje klasöründe `dotnet ef migrations add Ilk` komutunu " +
            "çalıştırıp oluşan Migrations klasörünü commit'leyin.");

    db.Database.Migrate();
}

app.Run();

// Aşama 4: Entegrasyon testleri (WebApplicationFactory<Program>) uygulamayı bellekte ayağa
// kaldırabilsin diye. Top-level statements'ın ürettiği Program sınıfı varsayılan olarak
// internal'dır; bu partial tanım onu public yapar. İçi bilerek boş.
public partial class Program { }
