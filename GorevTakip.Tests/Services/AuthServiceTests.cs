using GorevTakip.Dtos;
using GorevTakip.Infrastructure;
using GorevTakip.Services;
using GorevTakip.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace GorevTakip.Tests.Services;

// Gerçek EF Core + bellek içi SQLite + gerçek hash/token servisleriyle (HTTP olmadan).
// Her adım için ayrı DbContext kullanıyoruz (kayıt, giriş, doğrulama): üretimde de her istek
// kendi context'ini alır, EF'in önbelleği testi yanıltmaz.
public sealed class AuthServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly PasswordHasher _hasher = new(Options.Create(new PasswordHashingOptions { Iterations = 1000 }));
    private readonly TokenService _tokens = new(Options.Create(new JwtOptions
    {
        Key = "auth-servisi-testi-icin-gizli-anahtar-0123456789"
    }));

    public void Dispose() => _db.Dispose();

    // ---------- Kayıt ----------

    [Fact]
    public async Task RegisterAsync_StoresUserWithHashedPassword_AndReturnsToken()
    {
        using var context = _db.CreateContext();
        var service = new AuthService(context, _hasher, _tokens);

        var result = await service.RegisterAsync(new RegisterRequest("ali@ornek.com", "Sifre1234!"));

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Equal("ali@ornek.com", result.Email);

        using var verify = _db.CreateContext();
        var stored = await verify.Users.SingleAsync(u => u.Email == "ali@ornek.com");
        Assert.DoesNotContain("Sifre1234!", stored.PasswordHash);        // düz metin saklanmaz
        Assert.True(_hasher.Verify("Sifre1234!", stored.PasswordHash));  // ama doğrulanabilir
    }

    [Fact]
    public async Task RegisterAsync_NormalizesEmail()
    {
        using var context = _db.CreateContext();
        var service = new AuthService(context, _hasher, _tokens);

        var result = await service.RegisterAsync(new RegisterRequest("  Ali@ORNEK.com ", "Sifre1234!"));

        Assert.NotNull(result);
        Assert.Equal("ali@ornek.com", result.Email);

        using var verify = _db.CreateContext();
        Assert.True(await verify.Users.AnyAsync(u => u.Email == "ali@ornek.com"));
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ReturnsNullAndKeepsTheOriginalPassword()
    {
        using (var first = _db.CreateContext())
        {
            await new AuthService(first, _hasher, _tokens)
                .RegisterAsync(new RegisterRequest("ali@ornek.com", "Sifre1234!"));
        }

        using var second = _db.CreateContext();
        var result = await new AuthService(second, _hasher, _tokens)
            .RegisterAsync(new RegisterRequest("ALI@ornek.com", "BaskaSifre99"));

        Assert.Null(result);

        using var verify = _db.CreateContext();
        var stored = await verify.Users.SingleAsync(u => u.Email == "ali@ornek.com");
        Assert.True(_hasher.Verify("Sifre1234!", stored.PasswordHash));     // eski şifre geçerli
        Assert.False(_hasher.Verify("BaskaSifre99", stored.PasswordHash));  // hesap ele geçirilemedi
    }

    // ---------- Giriş ----------

    [Fact]
    public async Task LoginAsync_WithCorrectPassword_ReturnsToken()
    {
        await RegisterAsync("ali@ornek.com", "Sifre1234!");

        using var context = _db.CreateContext();
        var result = await new AuthService(context, _hasher, _tokens)
            .LoginAsync(new LoginRequest("ALI@ornek.com", "Sifre1234!"));

        Assert.NotNull(result);
        Assert.Equal("ali@ornek.com", result.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsNull()
    {
        await RegisterAsync("ali@ornek.com", "Sifre1234!");

        using var context = _db.CreateContext();
        var result = await new AuthService(context, _hasher, _tokens)
            .LoginAsync(new LoginRequest("ali@ornek.com", "YanlisSifre99"));

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ReturnsNull()
    {
        using var context = _db.CreateContext();

        var result = await new AuthService(context, _hasher, _tokens)
            .LoginAsync(new LoginRequest("yok@ornek.com", "Sifre1234!"));

        Assert.Null(result);
    }

    private async Task RegisterAsync(string email, string password)
    {
        using var context = _db.CreateContext();
        await new AuthService(context, _hasher, _tokens).RegisterAsync(new RegisterRequest(email, password));
    }
}
