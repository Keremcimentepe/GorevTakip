using System.Text;
using GorevTakip.Infrastructure;
using GorevTakip.Models;
using GorevTakip.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace GorevTakip.Tests.Services;

public class TokenServiceTests
{
    private const string Key = "token-servisi-testi-icin-gizli-anahtar-0123456789";

    private static readonly User Ali = new() { Id = 42, Email = "ali@ornek.com" };

    private static TokenService Create(int expiresMinutes = 60) =>
        new(Options.Create(new JwtOptions
        {
            Key = Key,
            Issuer = "test-yayinci",
            Audience = "test-kitle",
            ExpiresMinutes = expiresMinutes
        }));

    private static TokenValidationParameters ValidationFor(string key) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = "test-yayinci",
        ValidateAudience = true,
        ValidAudience = "test-kitle",
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
    };

    [Fact]
    public void CreateToken_CarriesUserIdAndEmail_AndReturnsThemInTheResponse()
    {
        var response = Create().CreateToken(Ali);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(response.Token);
        Assert.Equal("42", jwt.Subject);
        Assert.Equal("ali@ornek.com", jwt.GetClaim("email").Value);
        Assert.Equal("test-yayinci", jwt.Issuer);
        Assert.Contains("test-kitle", jwt.Audiences);
        Assert.Equal("ali@ornek.com", response.Email);
    }

    [Fact]
    public void CreateToken_ExpiresAfterTheConfiguredLifetime()
    {
        var before = DateTime.UtcNow;

        var response = Create(expiresMinutes: 30).CreateToken(Ali);

        var after = DateTime.UtcNow;
        Assert.InRange(response.ExpiresAt, before.AddMinutes(30), after.AddMinutes(30));

        // Token'ın içindeki "exp" de aynı anı gösterir (JWT saniye hassasiyetindedir)
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(response.Token);
        Assert.True(Math.Abs((jwt.ValidTo - response.ExpiresAt).TotalSeconds) < 2);
    }

    [Fact]
    public async Task CreateToken_ProducesATokenThatValidatesWithTheSameKey()
    {
        var response = Create().CreateToken(Ali);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(response.Token, ValidationFor(Key));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task CreateToken_ProducesATokenThatFailsWithAnotherKey()
    {
        var response = Create().CreateToken(Ali);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            response.Token, ValidationFor("baska-bir-gizli-anahtar-0123456789-abcdefghijklm"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateToken_DoesNotPutAnythingSecretInThePayload()
    {
        var response = Create().CreateToken(new User { Id = 1, Email = "ali@ornek.com", PasswordHash = "GIZLI-HASH" });

        // Payload base64 ile kodlu olduğu için ham metinde değil, çözülmüş talepler (claims) içinde ararız
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(response.Token);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Value.Contains("GIZLI-HASH"));
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type.Contains("assword", StringComparison.OrdinalIgnoreCase));
    }
}
