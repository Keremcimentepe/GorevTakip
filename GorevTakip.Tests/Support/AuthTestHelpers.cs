using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using GorevTakip.Dtos;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GorevTakip.Tests.Support;

public static class AuthTestHelpers
{
    public static Task<HttpResponseMessage> RegisterAsync(this HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/auth/register", new RegisterRequest(email, password), Json.Options);

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password), Json.Options);

    /// <summary>Cevabın gövdesini AuthResponse olarak okur.</summary>
    public static async Task<AuthResponse> ReadAuthAsync(this HttpResponseMessage response)
    {
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json.Options);
        return auth!;
    }

    /// <summary>İstemcinin bundan sonraki isteklerine verilen token'ı ekler.</summary>
    public static HttpClient WithBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

/// <summary>
/// Test için elle JWT üretir. Süresi dolmuş, yanlış anahtarla imzalı, yanlış "audience"lı gibi
/// bozuk token'ların API tarafından reddedildiğini sınamak için kullanılır.
/// </summary>
public static class TestTokens
{
    public static string Create(
        int userId = 1,
        string email = "kullanici@ornek.com",
        string key = GorevTakipFactory.JwtKey,
        string issuer = GorevTakipFactory.JwtIssuer,
        string audience = GorevTakipFactory.JwtAudience,
        DateTime? expires = null,
        DateTime? notBefore = null)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString(CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Email, email),
            }),
            Issuer = issuer,
            Audience = audience,
            NotBefore = notBefore,
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
