namespace GorevTakip.Infrastructure;

// Ayarlar appsettings.json / ortam değişkenlerinden okunur ("Jwt:Key" ya da Jwt__Key).
// Sınıf biçiminde tutmak, ayarları kodun her yerinde string anahtarlarla aramaktan iyidir.
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "GorevTakip";
    public string Audience { get; set; } = "GorevTakip";

    // Token'ları imzalamak için gizli anahtar. Koda ya da git'e GİRMEZ; sunucuda ortam
    // değişkeniyle verilir. HS256 için en az 32 karakter olmalı. Sızarsa herkes sahte token üretir.
    public string Key { get; set; } = string.Empty;

    // Token'ın geçerlilik süresi (dakika). Varsayılan 24 saat.
    public int ExpiresMinutes { get; set; } = 1440;
}

public sealed class PasswordHashingOptions
{
    public const string SectionName = "PasswordHashing";

    // PBKDF2 yineleme sayısı. Yüksek olması şifre kırmayı yavaşlatır (OWASP, PBKDF2-HMAC-SHA256 için
    // 600.000 öneriyor). Testlerde hız için düşürülür.
    public int Iterations { get; set; } = 600_000;
}

public static class RateLimitPolicies
{
    // Giriş/kayıt uç noktalarını kaba kuvvet (brute force) denemelerine karşı sınırlayan politika
    public const string Auth = "auth";
}
