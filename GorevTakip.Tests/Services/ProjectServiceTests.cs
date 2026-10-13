using GorevTakip.Dtos;
using GorevTakip.Models;
using GorevTakip.Services;
using GorevTakip.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GorevTakip.Tests.Services;

// Servis testleri: gerçek EF Core + bellek içi SQLite ile iş mantığını sınar. HTTP yok.
// xUnit her test için sınıfın yeni bir örneğini oluşturur; bu yüzden her testin temiz bir
// veritabanı (_db) vardır ve testler birbirini etkilemez.
// Aşama 5: Her çağrı "kim istiyor?" bilgisini alır; _db.UserId varsayılan kullanıcıdır.
public sealed class ProjectServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateAsync_TrimsFieldsAndPersistsProject()
    {
        using var context = _db.CreateContext();
        var service = new ProjectService(context);

        var result = await service.CreateAsync(_db.UserId, new CreateProjectRequest("  Ev işleri  ", "  Haftalık liste  "));

        Assert.True(result.Id > 0);
        Assert.Equal("Ev işleri", result.Name);
        Assert.Equal("Haftalık liste", result.Description);
        Assert.Equal(0, result.TaskCount);

        using var verify = _db.CreateContext();
        var stored = await verify.Projects.SingleAsync();
        Assert.Equal("Ev işleri", stored.Name);
        Assert.Equal("Haftalık liste", stored.Description);
    }

    [Fact]
    public async Task CreateAsync_AssignsProjectToTheCaller()
    {
        var otherUserId = _db.AddUser("baska@ornek.com");

        using var context = _db.CreateContext();
        var service = new ProjectService(context);

        await service.CreateAsync(otherUserId, new CreateProjectRequest("Başkasının projesi", null));

        using var verify = _db.CreateContext();
        Assert.Equal(otherUserId, (await verify.Projects.SingleAsync()).OwnerId);
    }

    [Fact]
    public async Task CreateAsync_WithoutDescription_StoresNull()
    {
        using var context = _db.CreateContext();
        var service = new ProjectService(context);

        var result = await service.CreateAsync(_db.UserId, new CreateProjectRequest("Ev işleri", null));

        Assert.Null(result.Description);
    }

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        using var context = _db.CreateContext();
        var service = new ProjectService(context);

        var result = await service.GetAllAsync(_db.UserId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsNewestFirst_WithTaskCounts()
    {
        // Sıralamayı rastlantıya bırakmamak için tarihleri elle veriyoruz.
        var olderId = _db.AddProject("Eski proje", new DateTime(2026, 1, 1));
        var newerId = _db.AddProject("Yeni proje", new DateTime(2026, 2, 1));
        _db.AddTask(olderId, "Görev 1");
        _db.AddTask(olderId, "Görev 2");

        using var context = _db.CreateContext();
        var service = new ProjectService(context);

        var result = await service.GetAllAsync(_db.UserId);

        Assert.Equal(new[] { "Yeni proje", "Eski proje" }, result.Select(p => p.Name));
        Assert.Equal(new[] { 0, 2 }, result.Select(p => p.TaskCount));
        Assert.Equal(newerId, result[0].Id);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyTheCallersProjects()
    {
        var otherUserId = _db.AddUser("baska@ornek.com");
        _db.AddProject("Benim projem");
        _db.AddProject("Başkasının projesi", ownerId: otherUserId);

        using var context = _db.CreateContext();
        var service = new ProjectService(context);

        var mine = await service.GetAllAsync(_db.UserId);
        var theirs = await service.GetAllAsync(otherUserId);

        Assert.Equal("Benim projem", Assert.Single(mine).Name);
        Assert.Equal("Başkasının projesi", Assert.Single(theirs).Name);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsProjectWithTasksInCreationOrder()
    {
        var projectId = _db.AddProject("Proje");
        _db.AddTask(projectId, "İkinci", createdAt: new DateTime(2026, 1, 2));
        _db.AddTask(projectId, "Birinci", createdAt: new DateTime(2026, 1, 1));

        using var context = _db.CreateContext();
        var service = new ProjectService(context);

        var result = await service.GetByIdAsync(_db.UserId, projectId);

        Assert.NotNull(result);
        Assert.Equal("Proje", result.Name);
        Assert.Equal(new[] { "Birinci", "İkinci" }, result.Tasks.Select(t => t.Title));
    }

    [Fact]
    public async Task GetByIdAsync_UnknownProject_ReturnsNull()
    {
        using var context = _db.CreateContext();
        var service = new ProjectService(context);

        var result = await service.GetByIdAsync(_db.UserId, 999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ProjectOfAnotherUser_ReturnsNull()
    {
        var otherUserId = _db.AddUser("baska@ornek.com");
        var theirProjectId = _db.AddProject("Başkasının projesi", ownerId: otherUserId);

        using var context = _db.CreateContext();
        var service = new ProjectService(context);

        // Başkasının projesi, var olmayan proje gibi davranır
        Assert.Null(await service.GetByIdAsync(_db.UserId, theirProjectId));
        Assert.NotNull(await service.GetByIdAsync(otherUserId, theirProjectId));
    }

    [Fact]
    public async Task UpdateAsync_ChangesAndTrimsFields()
    {
        var projectId = _db.AddProject("Eski ad");

        using var context = _db.CreateContext();
        var service = new ProjectService(context);

        var updated = await service.UpdateAsync(_db.UserId, projectId, new UpdateProjectRequest("  Yeni ad  ", " Açıklama "));

        Assert.True(updated);

        using var verify = _db.CreateContext();
        var stored = await verify.Projects.SingleAsync();
        Assert.Equal("Yeni ad", stored.Name);
        Assert.Equal("Açıklama", stored.Description);
    }

    [Fact]
    public async Task UpdateAsync_UnknownProject_ReturnsFalse()
    {
        using var context = _db.CreateContext();
        var service = new ProjectService(context);

        var updated = await service.UpdateAsync(_db.UserId, 999, new UpdateProjectRequest("Ad", null));

        Assert.False(updated);
    }

    [Fact]
    public async Task UpdateAsync_ProjectOfAnotherUser_ReturnsFalseAndChangesNothing()
    {
        var otherUserId = _db.AddUser("baska@ornek.com");
        var theirProjectId = _db.AddProject("Başkasının projesi", ownerId: otherUserId);

        using var context = _db.CreateContext();
        var service = new ProjectService(context);

        var updated = await service.UpdateAsync(_db.UserId, theirProjectId, new UpdateProjectRequest("Ele geçirildi", null));

        Assert.False(updated);

        using var verify = _db.CreateContext();
        Assert.Equal("Başkasının projesi", (await verify.Projects.SingleAsync()).Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesProjectAndItsTasks()
    {
        var projectId = _db.AddProject("Silinecek");
        var otherId = _db.AddProject("Kalacak");
        _db.AddTask(projectId, "Silinecek görev 1");
        _db.AddTask(projectId, "Silinecek görev 2");
        _db.AddTask(otherId, "Kalacak görev");

        using var context = _db.CreateContext();
        var service = new ProjectService(context);

        var deleted = await service.DeleteAsync(_db.UserId, projectId);

        Assert.True(deleted);

        using var verify = _db.CreateContext();
        Assert.Equal("Kalacak", (await verify.Projects.SingleAsync()).Name);
        // Cascade: projeyle birlikte görevleri de gitti, diğer projenin görevi yerinde
        Assert.Equal("Kalacak görev", (await verify.Tasks.SingleAsync()).Title);
    }

    [Fact]
    public async Task DeleteAsync_UnknownProject_ReturnsFalse()
    {
        using var context = _db.CreateContext();
        var service = new ProjectService(context);

        var deleted = await service.DeleteAsync(_db.UserId, 999);

        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteAsync_ProjectOfAnotherUser_ReturnsFalseAndKeepsTheProject()
    {
        var otherUserId = _db.AddUser("baska@ornek.com");
        var theirProjectId = _db.AddProject("Başkasının projesi", ownerId: otherUserId);
        _db.AddTask(theirProjectId, "Başkasının görevi");

        using var context = _db.CreateContext();
        var service = new ProjectService(context);

        var deleted = await service.DeleteAsync(_db.UserId, theirProjectId);

        Assert.False(deleted);

        using var verify = _db.CreateContext();
        Assert.Single(await verify.Projects.ToListAsync());
        Assert.Single(await verify.Tasks.ToListAsync());
    }
}
