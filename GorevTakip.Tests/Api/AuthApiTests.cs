using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GorevTakip.Dtos;
using GorevTakip.Tests.Support;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace GorevTakip.Tests.Api;

// Giriş/kayıt uç noktaları ve "token olmadan / bozuk token ile girilemez" kuralı.
public sealed class AuthApiTests : IDisposable
{
    private const string Email = "ali@ornek.com";
    private const string Password = "Sifre1234!";

    private readonly GorevTakipFactory _factory = new();
    private readonly HttpClient _client; // girişsiz (anonim) istemci

    public AuthApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ---------- POST /auth/register ----------

    [Fact]
    public async Task Register_WithValidBody_Returns201WithUsableToken()
    {
        var response = await _client.RegisterAsync(Email, Password);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var auth = await response.ReadAuthAsync();
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.Equal(Email, auth.Email);
        Assert.True(auth.ExpiresAt > DateTime.UtcNow);

        // Token gerçekten çalışıyor mu: korumalı bir uç noktadan 200 almalıyız
        using var authed = _factory.CreateClient().WithBearer(auth.Token);
        Assert.Equal(HttpStatusCode.OK, (await authed.GetAsync("/projects")).StatusCode);
    }

    [Fact]
    public async Task Register_NormalizesEmail_ToLowerCaseAndTrimmed()
    {
        var response = await _client.RegisterAsync("  Ali@Ornek.COM  ", Password);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("ali@ornek.com", (await response.ReadAuthAsync()).Email);
    }

    [Fact]
    public async Task Register_SameEmailTwice_Returns409()
    {
        await _client.RegisterAsync(Email, Password);

        // Büyük/küçük harf farkı aynı hesap sayılır
        var second = await _client.RegisterAsync("ALI@ornek.com", "BaskaSifre99");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData("", "Sifre1234!", "E-posta boş olamaz.")]
    [InlineData("gecersiz-adres", "Sifre1234!", "Geçerli bir e-posta adresi girin.")]
    [InlineData("ali@ornek.com", "", "Şifre boş olamaz.")]
    [InlineData("ali@ornek.com", "kisa", "Şifre en az 8 karakter olmalı.")]
    public async Task Register_WithInvalidBody_Returns400WithMessage(string email, string password, string expectedMessage)
    {
        var response = await _client.RegisterAsync(email, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedMessage, await response.ReadValidationMessagesAsync());
    }

    [Fact]
    public async Task Register_WhenInvalid_CreatesNoAccount()
    {
        await _client.RegisterAsync(Email, "kisa");

        // Hesap açılmadıysa aynı e-postayla geçerli şifreyle kayıt hâlâ mümkün olmalı
        var retry = await _client.RegisterAsync(Email, Password);

        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    // ---------- POST /auth/login ----------

    [Fact]
    public async Task Login_WithCorrectCredentials_Returns200WithToken()
    {
        await _client.RegisterAsync(Email, Password);

        var response = await _client.LoginAsync(Email, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var auth = await response.ReadAuthAsync();
        using var authed = _factory.CreateClient().WithBearer(auth.Token);
        Assert.Equal(HttpStatusCode.OK, (await authed.GetAsync("/projects")).StatusCode);
    }

    [Fact]
    public async Task Login_IgnoresEmailCaseAndSurroundingSpaces()
    {
        await _client.RegisterAsync(Email, Password);

        var response = await _client.LoginAsync("  ALI@Ornek.com ", Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        await _client.RegisterAsync(Email, Password);

        var response = await _client.LoginAsync(Email, "YanlisSifre99");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401_WithSameMessageAsWrongPassword()
    {
        // "Bu e-posta kayıtlı değil" ile "şifre yanlış" aynı cevabı vermeli; yoksa saldırgan
        // hangi e-postaların kayıtlı olduğunu öğrenebilir.
        await _client.RegisterAsync(Email, Password);

        var wrongPassword = await _client.LoginAsync(Email, "YanlisSifre99");
        var unknownEmail = await _client.LoginAsync("yok@ornek.com", Password);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        // Tüm gövdeyi değil başlığı karşılaştırıyoruz: gövdede isteğe özel izleme kimliği olabilir
        Assert.Equal("E-posta veya şifre hatalı.", await ReadTitleAsync(wrongPassword));
        Assert.Equal("E-posta veya şifre hatalı.", await ReadTitleAsync(unknownEmail));
    }

    private static async Task<string?> ReadTitleAsync(HttpResponseMessage response)
    {
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("title").GetString();
    }

    [Fact]
    public async Task Login_WithBlankFields_Returns400()
    {
        var response = await _client.LoginAsync("", "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var messages = await response.ReadValidationMessagesAsync();
        Assert.Contains("E-posta boş olamaz.", messages);
        Assert.Contains("Şifre boş olamaz.", messages);
    }

    [Fact]
    public async Task Passwords_AreNeverReturnedInResponses()
    {
        var response = await _client.RegisterAsync(Email, Password);

        Assert.DoesNotContain(Password, await response.Content.ReadAsStringAsync());
    }

    // ---------- Korumalı uç noktalar ----------

    [Theory]
    [InlineData("GET", "/projects")]
    [InlineData("POST", "/projects")]
    [InlineData("GET", "/projects/1")]
    [InlineData("PUT", "/projects/1")]
    [InlineData("DELETE", "/projects/1")]
    [InlineData("GET", "/projects/1/tasks")]
    [InlineData("POST", "/projects/1/tasks")]
    [InlineData("GET", "/tasks/1")]
    [InlineData("PUT", "/tasks/1")]
    [InlineData("PATCH", "/tasks/1/status")]
    [InlineData("DELETE", "/tasks/1")]
    public async Task ProtectedEndpoint_WithoutToken_Returns401(string method, string url)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithGarbageToken_Returns401()
    {
        using var client = _factory.CreateClient().WithBearer("bu-bir-token-degil");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/projects")).StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTokenSignedByAnotherKey_Returns401()
    {
        var forged = TestTokens.Create(key: "baska-bir-gizli-anahtar-0123456789-abcdefghij");
        using var client = _factory.CreateClient().WithBearer(forged);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/projects")).StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithExpiredToken_Returns401()
    {
        var expired = TestTokens.Create(
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1));
        using var client = _factory.CreateClient().WithBearer(expired);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/projects")).StatusCode);
    }

    [Theory]
    [InlineData("baska-yayinci", GorevTakipFactory.JwtAudience)]
    [InlineData(GorevTakipFactory.JwtIssuer, "baska-hedef-kitle")]
    public async Task ProtectedEndpoint_WithWrongIssuerOrAudience_Returns401(string issuer, string audience)
    {
        var token = TestTokens.Create(issuer: issuer, audience: audience);
        using var client = _factory.CreateClient().WithBearer(token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/projects")).StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTokenWithoutSignature_Returns401()
    {
        // "alg: none" saldırısı: imzasız token'ı kabul eden bir sunucu herkese kapıyı açar
        var valid = TestTokens.Create();
        var unsigned = valid[..valid.LastIndexOf('.')] + "."; // imza bölümünü at
        using var client = _factory.CreateClient().WithBearer(unsigned);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/projects")).StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTamperedPayload_Returns401()
    {
        // Payload'daki kullanıcı kimliğini elle değiştirmek imzayı bozar
        var valid = TestTokens.Create(userId: 1);
        var parts = valid.Split('.');
        var forgedPayload = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes("{\"sub\":\"2\",\"email\":\"x@ornek.com\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var tampered = $"{parts[0]}.{forgedPayload}.{parts[2]}";
        using var client = _factory.CreateClient().WithBearer(tampered);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/projects")).StatusCode);
    }

    [Fact]
    public async Task IssuedToken_ContainsUserIdAndEmail_ButNoPassword()
    {
        var auth = await (await _client.RegisterAsync(Email, Password)).ReadAuthAsync();

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(auth.Token);

        Assert.Equal(Email, jwt.GetClaim("email").Value);
        Assert.True(int.TryParse(jwt.Subject, out var userId) && userId > 0);
        Assert.DoesNotContain(Password, auth.Token);
    }

    // ---------- Herkese açık uç noktalar ----------

    [Fact]
    public async Task Health_IsPublic_AndReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

// Hız sınırı: art arda çok sayıda giriş denemesi 429 ile durdurulur (şifre tahmin saldırısına karşı).
public sealed class AuthRateLimitTests : IDisposable
{
    private readonly GorevTakipFactory _factory = new(extraSettings: new Dictionary<string, string?>
    {
        ["RateLimiting:AuthPerMinute"] = "3"
    });

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Login_AfterTooManyAttempts_Returns429()
    {
        using var client = _factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            statuses.Add((await client.LoginAsync("ali@ornek.com", "YanlisSifre99")).StatusCode);

        Assert.Equal(new[]
        {
            HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized,
            HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests
        }, statuses);
    }

    [Fact]
    public async Task ProjectEndpoints_AreNotRateLimited()
    {
        using var client = _factory.CreateAuthenticatedClient(); // kayıt: 1 istek sayılır

        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/projects")).StatusCode);
    }
}
