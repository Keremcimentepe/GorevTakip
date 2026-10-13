using System.Globalization;
using System.Security.Cryptography;
using GorevTakip.Infrastructure;
using Microsoft.Extensions.Options;

namespace GorevTakip.Services;

// PBKDF2-HMAC-SHA256 ile şifre hash'leme. Şifreleri düz metin ya da düz SHA256 olarak saklamak
// yaygın bir güvenlik hatasıdır: hızlı hash'ler saniyede milyarlarca kez denenebilir. PBKDF2 bilerek
// yavaştır ve her şifreye ayrı rastgele "tuz" (salt) ekler; aynı şifreyi kullanan iki kullanıcının
// hash'i farklı çıkar.
//
// Saklanan biçim: PBKDF2-SHA256$yineleme$tuz(base64)$hash(base64)
// Yineleme sayısı hash'in içinde durur; ileride sayıyı artırırsak eski hash'ler yine doğrulanabilir.
public sealed class PasswordHasher : IPasswordHasher
{
    private const string Prefix = "PBKDF2-SHA256";
    private const int SaltSize = 16;  // bayt
    private const int KeySize = 32;   // bayt

    private readonly int _iterations;

    public PasswordHasher(IOptions<PasswordHashingOptions> options)
    {
        _iterations = options.Value.Iterations;
    }

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, HashAlgorithmName.SHA256, KeySize);

        return string.Join('$', Prefix, _iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    public bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split('$');

        if (parts.Length != 4 || parts[0] != Prefix)
            return false;

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) || iterations <= 0)
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expected.Length == 0)
            return false;

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        // Sabit zamanlı karşılaştırma: sonucun kaç bayt tuttuğuna bakarak şifre tahmin edilemesin
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
