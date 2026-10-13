using System.Security.Claims;
using GorevTakip.Infrastructure;
using Xunit;

namespace GorevTakip.Tests.Infrastructure;

public class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "test"));

    [Fact]
    public void GetUserId_ReadsTheSubClaim()
    {
        var principal = PrincipalWith(new Claim("sub", "42"));

        Assert.Equal(42, principal.GetUserId());
    }

    [Fact]
    public void GetUserId_FallsBackToNameIdentifier()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "7"));

        Assert.Equal(7, principal.GetUserId());
    }

    [Fact]
    public void GetUserId_PrefersSubWhenBothExist()
    {
        var principal = PrincipalWith(new Claim("sub", "1"), new Claim(ClaimTypes.NameIdentifier, "2"));

        Assert.Equal(1, principal.GetUserId());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("99999999999999999999")]
    public void GetUserId_WithMissingOrInvalidClaim_Throws(string? value)
    {
        var principal = value is null ? PrincipalWith() : PrincipalWith(new Claim("sub", value));

        Assert.Throws<InvalidOperationException>(() => principal.GetUserId());
    }
}
