using Npgsql;

namespace GorevTakip.Infrastructure;

// Barındırma servisleri (Render, Neon, Heroku...) veritabanı adresini genelde URL olarak verir:
//   postgresql://kullanici:sifre@sunucu:5432/veritabani?sslmode=require
// Npgsql ise "Host=...;Database=..." biçimini bekler. Bu sınıf URL'yi Npgsql biçimine çevirir;
// zaten "Host=..." biçimindeyse olduğu gibi bırakır.
public static class PostgresConnectionString
{
    public static string Normalize(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var uri = new Uri(value);

        // UserInfo "kullanici:sifre" biçimindedir; şifrede özel karakter varsa %XX ile kaçışlanmıştır
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(userInfo[0]),
        };

        if (userInfo.Length > 1)
            builder.Password = Uri.UnescapeDataString(userInfo[1]);

        var database = uri.AbsolutePath.Trim('/');
        if (database.Length > 0)
            builder.Database = Uri.UnescapeDataString(database);

        // ?sslmode=require gibi parametre varsa Npgsql karşılığına çevir ("verify-full" -> VerifyFull)
        var sslMode = GetQueryValue(uri.Query, "sslmode");
        if (sslMode is not null && Enum.TryParse<SslMode>(sslMode.Replace("-", ""), ignoreCase: true, out var parsed))
            builder.SslMode = parsed;

        return builder.ConnectionString;
    }

    private static string? GetQueryValue(string query, string key)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);

            if (parts.Length == 2 && parts[0].Equals(key, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(parts[1]);
        }

        return null;
    }
}
