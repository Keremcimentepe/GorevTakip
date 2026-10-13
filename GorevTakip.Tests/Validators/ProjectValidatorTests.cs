using FluentValidation.TestHelper;
using GorevTakip.Dtos;
using GorevTakip.Validators;
using Xunit;

namespace GorevTakip.Tests.Validators;

// Validator'lar saf mantıktır: veritabanı da HTTP de gerekmez. En hızlı ve en kolay test türü budur.
public class ProjectValidatorTests
{
    private readonly CreateProjectRequestValidator _create = new();
    private readonly UpdateProjectRequestValidator _update = new();

    [Fact]
    public void Create_ValidRequest_HasNoErrors()
    {
        var result = _create.TestValidate(new CreateProjectRequest("Web sitesi", "Kurumsal site"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Create_NullDescription_IsAllowed()
    {
        var result = _create.TestValidate(new CreateProjectRequest("Web sitesi", null));

        result.ShouldNotHaveAnyValidationErrors();
    }

    // Theory: aynı testi farklı girdilerle çalıştırır (her InlineData ayrı bir test olarak görünür)
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_Fails(string name)
    {
        var result = _create.TestValidate(new CreateProjectRequest(name, null));

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Proje adı boş olamaz.");
    }

    [Fact]
    public void Create_NameAtMaxLength_Passes()
    {
        var result = _create.TestValidate(new CreateProjectRequest(new string('a', 100), null));

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Create_NameOverMaxLength_Fails()
    {
        var result = _create.TestValidate(new CreateProjectRequest(new string('a', 101), null));

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Proje adı en fazla 100 karakter olabilir.");
    }

    [Fact]
    public void Create_DescriptionOverMaxLength_Fails()
    {
        var result = _create.TestValidate(new CreateProjectRequest("Web sitesi", new string('a', 501)));

        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Açıklama en fazla 500 karakter olabilir.");
    }

    [Fact]
    public void Update_ValidRequest_HasNoErrors()
    {
        var result = _update.TestValidate(new UpdateProjectRequest("Yeni ad", "Yeni açıklama"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Update_BlankName_Fails()
    {
        var result = _update.TestValidate(new UpdateProjectRequest("", null));

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Proje adı boş olamaz.");
    }

    [Fact]
    public void Update_NameOverMaxLength_Fails()
    {
        var result = _update.TestValidate(new UpdateProjectRequest(new string('a', 101), null));

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Proje adı en fazla 100 karakter olabilir.");
    }

    [Fact]
    public void Update_DescriptionOverMaxLength_Fails()
    {
        var result = _update.TestValidate(new UpdateProjectRequest("Ad", new string('a', 501)));

        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Açıklama en fazla 500 karakter olabilir.");
    }
}
