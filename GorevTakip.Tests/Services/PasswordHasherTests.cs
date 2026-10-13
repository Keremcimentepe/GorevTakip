using GorevTakip.Infrastructure;
using GorevTakip.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace GorevTakip.Tests.Services;

public class PasswordHasherTests
{
    // Testlerde düşük yineleme sayısı: davranış aynı, ama saniyeler yerine milisaniyeler sürer
    private static PasswordHasher Create(int iterations = 1000) =>
        new(Options.Create(new PasswordHashingOptions { Iterations = iterations }));

    [Fact]
    public void Verify_WithTheSamePassword_ReturnsTrue()
    {
        var hasher = Create();

        var hash = hasher.Hash("Sifre1234!");

        Assert.True(hasher.Verify("Sifre1234!", hash));
    }

    [Fact]
    public void Verify_WithAnotherPassword_ReturnsFalse()
    {
        var hasher = Create();

        var hash = hasher.Hash("Sifre1234!");

        Assert.False(hasher.Verify("sifre1234!", hash)); // büyük/küçük harf farkı bile yetmez
        Assert.False(hasher.Verify("Sifre1234", hash));
        Assert.False(hasher.Verify("", hash));
    }

    [Fact]
    public void Hash_OfTheSamePasswordTwice_DiffersBecauseOfRandomSalt()
    {
        var hasher = Create();

        var first = hasher.Hash("Sifre1234!");
        var second = hasher.Hash("Sifre1234!");

        Assert.NotEqual(first, second);
        // ...ama ikisi de doğrulanır
        Assert.True(hasher.Verify("Sifre1234!", first));
        Assert.True(hasher.Verify("Sifre1234!", second));
    }

    [Fact]
    public void Hash_DoesNotContainThePlainPassword()
    {
        var hash = Create().Hash("Sifre1234!");

        Assert.DoesNotContain("Sifre1234!", hash);
    }

    [Fact]
    public void Hash_HasTheDocumentedFormat()
    {
        var hash = Create(iterations: 1234).Hash("Sifre1234!");

        var parts = hash.Split('$');
        Assert.Equal(4, parts.Length);
        Assert.Equal("PBKDF2-SHA256", parts[0]);
        Assert.Equal("1234", parts[1]);
        Assert.Equal(16, Convert.FromBase64String(parts[2]).Length); // tuz
        Assert.Equal(32, Convert.FromBase64String(parts[3]).Length); // hash
    }

    [Fact]
    public void Verify_UsesTheIterationCountStoredInTheHash()
    {
        // Yineleme sayısını ileride artırsak bile eski hash'ler çalışmaya devam etmeli
        var oldHash = Create(iterations: 1000).Hash("Sifre1234!");

        var newerHasher = Create(iterations: 2000);

        Assert.True(newerHasher.Verify("Sifre1234!", oldHash));
    }

    [Fact]
    public void Verify_WorksWithNonAsciiPasswords()
    {
        var hasher = Create();
        const string password = "şifre-ĞÜŞİÖÇ-🔑-123";

        var hash = hasher.Hash(password);

        Assert.True(hasher.Verify(password, hash));
        Assert.False(hasher.Verify("sifre-GUSIOC-123", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("duz-metin-sifre")]
    [InlineData("PBKDF2-SHA256$1000$AAAA")]                       // parça eksik
    [InlineData("PBKDF2-SHA256$1000$AAAA$AAAA$AAAA")]             // parça fazla
    [InlineData("BASKA-ALGORITMA$1000$AAAA$AAAA")]                // tanınmayan algoritma
    [InlineData("PBKDF2-SHA256$abc$AAAA$AAAA")]                   // yineleme sayı değil
    [InlineData("PBKDF2-SHA256$0$AAAA$AAAA")]                     // yineleme sıfır
    [InlineData("PBKDF2-SHA256$-5$AAAA$AAAA")]                    // yineleme negatif
    [InlineData("PBKDF2-SHA256$1000$!!!$AAAA")]                   // tuz geçerli base64 değil
    [InlineData("PBKDF2-SHA256$1000$AAAA$!!!")]                   // hash geçerli base64 değil
    [InlineData("PBKDF2-SHA256$1000$$AAAA")]                      // tuz boş
    [InlineData("PBKDF2-SHA256$1000$AAAA$")]                      // hash boş: boş hash her şifreyi kabul etmemeli
    public void Verify_WithMalformedHash_ReturnsFalse_AndDoesNotThrow(string storedHash)
    {
        var hasher = Create();

        Assert.False(hasher.Verify("Sifre1234!", storedHash));
    }

    [Fact]
    public void Verify_WithWellFormedButUnrelatedHash_ReturnsFalse()
    {
        Assert.False(Create().Verify("Sifre1234!", "PBKDF2-SHA256$1000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="));
    }
}
