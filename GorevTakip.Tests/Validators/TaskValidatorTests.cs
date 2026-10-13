using FluentValidation.TestHelper;
using GorevTakip.Dtos;
using GorevTakip.Models;
using GorevTakip.Validators;
using Xunit;

namespace GorevTakip.Tests.Validators;

public class TaskValidatorTests
{
    private readonly CreateTaskRequestValidator _create = new();
    private readonly UpdateTaskRequestValidator _update = new();
    private readonly UpdateTaskStatusRequestValidator _updateStatus = new();

    // ---------- Görev oluşturma ----------

    [Fact]
    public void Create_ValidRequest_HasNoErrors()
    {
        var result = _create.TestValidate(new CreateTaskRequest("Rapor yaz", "Cuma gününe kadar"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankTitle_Fails(string title)
    {
        var result = _create.TestValidate(new CreateTaskRequest(title, null));

        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Görev başlığı boş olamaz.");
    }

    [Fact]
    public void Create_TitleAtMaxLength_Passes()
    {
        var result = _create.TestValidate(new CreateTaskRequest(new string('a', 200), null));

        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Create_TitleOverMaxLength_Fails()
    {
        var result = _create.TestValidate(new CreateTaskRequest(new string('a', 201), null));

        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Görev başlığı en fazla 200 karakter olabilir.");
    }

    [Fact]
    public void Create_DescriptionOverMaxLength_Fails()
    {
        var result = _create.TestValidate(new CreateTaskRequest("Başlık", new string('a', 1001)));

        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Açıklama en fazla 1000 karakter olabilir.");
    }

    // ---------- Görev güncelleme ----------

    [Fact]
    public void Update_ValidRequest_HasNoErrors()
    {
        var result = _update.TestValidate(new UpdateTaskRequest("Başlık", null, TaskItemStatus.InProgress));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Update_BlankTitle_Fails()
    {
        var result = _update.TestValidate(new UpdateTaskRequest("", null, TaskItemStatus.Todo));

        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Görev başlığı boş olamaz.");
    }

    [Fact]
    public void Update_UnknownStatus_Fails()
    {
        // Enum aslında bir sayıdır; (TaskItemStatus)99 derleyiciyi geçer ama tanımlı bir değer değildir.
        var result = _update.TestValidate(new UpdateTaskRequest("Başlık", null, (TaskItemStatus)99));

        result.ShouldHaveValidationErrorFor(x => x.Status)
            .WithErrorMessage("Geçersiz görev durumu.");
    }

    // ---------- Sadece durum değiştirme ----------

    [Theory]
    [InlineData(TaskItemStatus.Todo)]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Done)]
    public void UpdateStatus_KnownStatus_Passes(TaskItemStatus status)
    {
        var result = _updateStatus.TestValidate(new UpdateTaskStatusRequest(status));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(99)]
    public void UpdateStatus_UnknownStatus_Fails(int value)
    {
        var result = _updateStatus.TestValidate(new UpdateTaskStatusRequest((TaskItemStatus)value));

        result.ShouldHaveValidationErrorFor(x => x.Status)
            .WithErrorMessage("Geçersiz görev durumu.");
    }
}
