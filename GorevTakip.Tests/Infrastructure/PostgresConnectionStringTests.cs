using GorevTakip.Infrastructure;
using Npgsql;
using Xunit;

namespace GorevTakip.Tests.Infrastructure;

public class PostgresConnectionStringTests
{
    [Fact]
    public void Normalize_LeavesKeyValueConnectionStringsUntouched()
    {
        const string value = "Host=db;Port=5432;Database=gorevtakip;Username=u;Password=p";

        Assert.Equal(value, PostgresConnectionString.Normalize(value));
    }

    [Fact]
    public void Normalize_ConvertsUrlToNpgsqlConnectionString()
    {
        var result = PostgresConnectionString.Normalize("postgresql://ali:gizli@db.ornek.com:5433/gorevtakip");

        var parsed = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("db.ornek.com", parsed.Host);
        Assert.Equal(5433, parsed.Port);
        Assert.Equal("gorevtakip", parsed.Database);
        Assert.Equal("ali", parsed.Username);
        Assert.Equal("gizli", parsed.Password);
    }

    [Fact]
    public void Normalize_AcceptsPostgresSchemeAndUsesDefaultPort()
    {
        var result = PostgresConnectionString.Normalize("postgres://ali:gizli@db.ornek.com/gorevtakip");

        var parsed = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("db.ornek.com", parsed.Host);
        Assert.Equal(5432, parsed.Port);
    }

    [Fact]
    public void Normalize_SchemeIsCaseInsensitive()
    {
        var result = PostgresConnectionString.Normalize("POSTGRESQL://ali:gizli@db.ornek.com/gorevtakip");

        Assert.Equal("db.ornek.com", new NpgsqlConnectionStringBuilder(result).Host);
    }

    [Fact]
    public void Normalize_DecodesSpecialCharactersInUserAndPassword()
    {
        // Şifre: p@ss;w/ord:1  (adreste % ile kaçışlanmış halde gelir)
        var result = PostgresConnectionString.Normalize(
            "postgresql://ali%40ornek:p%40ss%3Bw%2Ford%3A1@db.ornek.com/gorevtakip");

        var parsed = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("ali@ornek", parsed.Username);
        Assert.Equal("p@ss;w/ord:1", parsed.Password);
    }

    [Theory]
    [InlineData("require", SslMode.Require)]
    [InlineData("disable", SslMode.Disable)]
    [InlineData("verify-full", SslMode.VerifyFull)]
    [InlineData("verify-ca", SslMode.VerifyCA)]
    public void Normalize_MapsSslModeFromQueryString(string urlValue, SslMode expected)
    {
        var result = PostgresConnectionString.Normalize(
            $"postgresql://ali:gizli@db.ornek.com/gorevtakip?sslmode={urlValue}");

        Assert.Equal(expected, new NpgsqlConnectionStringBuilder(result).SslMode);
    }

    [Fact]
    public void Normalize_IgnoresUnknownQueryParameters()
    {
        // Neon gibi servisler adrese channel_binding gibi ek parametreler koyar
        var result = PostgresConnectionString.Normalize(
            "postgresql://ali:gizli@db.ornek.com/gorevtakip?sslmode=require&channel_binding=require&foo=bar");

        var parsed = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("db.ornek.com", parsed.Host);
        Assert.Equal(SslMode.Require, parsed.SslMode);
    }

    [Fact]
    public void Normalize_WithoutSslModeParameter_KeepsNpgsqlDefault()
    {
        var result = PostgresConnectionString.Normalize("postgresql://ali:gizli@db.ornek.com/gorevtakip");

        Assert.Equal(new NpgsqlConnectionStringBuilder().SslMode, new NpgsqlConnectionStringBuilder(result).SslMode);
    }
}
