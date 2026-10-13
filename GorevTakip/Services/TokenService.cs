using System.Globalization;
using System.Security.Claims;
using System.Text;
using GorevTakip.Dtos;
using GorevTakip.Infrastructure;
using GorevTakip.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GorevTakip.Services;

// JWT (JSON Web Token): "kim olduğunu" taşıyan, sunucunun gizli anahtarıyla imzalanmış küçük bir metin.
// Sunucu her istekte imzayı doğrular; veritabanında oturum tutmaz. Token'ın içeriği (payload) şifreli
// değil, sadece imzalıdır: içine şifre gibi gizli bir şey KOYMAYIZ.
public class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public AuthResponse CreateToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.ExpiresMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                // "sub": token'ın sahibi (kullanıcı kimliği). API her istekte kullanıcıyı buradan tanır.
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
            }),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
                SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return new AuthResponse(token, expiresAt, user.Email);
    }
}
