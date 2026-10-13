using FluentValidation.TestHelper;
using GorevTakip.Dtos;
using GorevTakip.Validators;
using Xunit;

namespace GorevTakip.Tests.Validators;

public class AuthValidatorTests
{
    private readonly RegisterRequestValidator _register = new();
    private readonly LoginRequestValidator _login = new();

    // ---------- Kayıt ----------

    [Fact]
    public void Register_ValidRequest_HasNoErrors()
    {
        var result = _register.TestValidate(new RegisterRequest("ali@ornek.com", "Sifre1234!"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_BlankEmail_FailsWithSingleMessage(string email)
    {
        var result = _register.TestValidate(new RegisterRequest(email, "Sifre1234!"));

        // CascadeMode.Stop: "boş olamaz" varken "geçerli adres girin" ayrıca eklenmez
        var error = Assert.Single(result.ShouldHaveValidationErrorFor(x => x.Email));
        Assert.Equal("E-posta boş olamaz.", error.ErrorMessage);
    }

    [Theory]
    [InlineData("adres-degil")]
    [InlineData("@ornek.com")]
    [InlineData("ali@")]
    [InlineData("a@b@c.com")]
    public void Register_MalformedEmail_Fails(string email)
    {
        var result = _register.TestValidate(new RegisterRequest(email, "Sifre1234!"));

        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Geçerli bir e-posta adresi girin.");
    }

    [Fact]
    public void Register_EmailAtMaxLength_Passes()
    {
        var email = new string('a', 254 - "@ornek.com".Length) + "@ornek.com"; // tam 254 karakter

        var result = _register.TestValidate(new RegisterRequest(email, "Sifre1234!"));

        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Register_EmailOverMaxLength_Fails()
    {
        var email = new string('a', 255 - "@ornek.com".Length) + "@ornek.com"; // 255 karakter

        var result = _register.TestValidate(new RegisterRequest(email, "Sifre1234!"));

        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("E-posta en fazla 254 karakter olabilir.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("        ")] // 8 boşluk: uzunluğu yeter ama içi boş
    public void Register_BlankPassword_FailsWithSingleMessage(string password)
    {
        var result = _register.TestValidate(new RegisterRequest("ali@ornek.com", password));

        var error = Assert.Single(result.ShouldHaveValidationErrorFor(x => x.Password));
        Assert.Equal("Şifre boş olamaz.", error.ErrorMessage);
    }

    [Fact]
    public void Register_PasswordOneCharacterShort_Fails()
    {
        var result = _register.TestValidate(new RegisterRequest("ali@ornek.com", new string('a', 7)));

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Şifre en az 8 karakter olmalı.");
    }

    [Fact]
    public void Register_PasswordAtBounds_Passes()
    {
        _register.TestValidate(new RegisterRequest("ali@ornek.com", new string('a', 8)))
            .ShouldNotHaveValidationErrorFor(x => x.Password);
        _register.TestValidate(new RegisterRequest("ali@ornek.com", new string('a', 128)))
            .ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Register_PasswordOverMaxLength_Fails()
    {
        var result = _register.TestValidate(new RegisterRequest("ali@ornek.com", new string('a', 129)));

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Şifre en fazla 128 karakter olabilir.");
    }

    // ---------- Giriş ----------

    [Fact]
    public void Login_ValidRequest_HasNoErrors()
    {
        _login.TestValidate(new LoginRequest("ali@ornek.com", "herhangi")).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Login_BlankFields_Fail()
    {
        var result = _login.TestValidate(new LoginRequest("", ""));

        result.ShouldHaveValidationErrorFor(x => x.Email).WithErrorMessage("E-posta boş olamaz.");
        result.ShouldHaveValidationErrorFor(x => x.Password).WithErrorMessage("Şifre boş olamaz.");
    }

    [Fact]
    public void Login_DoesNotApplyPasswordLengthRules()
    {
        // Kısa şifre "format hatası" değil "yanlış şifre" olarak işlenmeli (ipucu vermemek için)
        _login.TestValidate(new LoginRequest("ali@ornek.com", "a"))
            .ShouldNotHaveValidationErrorFor(x => x.Password);
    }
}
