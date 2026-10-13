using GorevTakip.Dtos;
using GorevTakip.Models;
using GorevTakip.Services;
using GorevTakip.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GorevTakip.Tests.Services;

// Aşama 5: Bir görevin sahibi, bağlı olduğu projenin sahibidir. _db.UserId varsayılan kullanıcıdır;
// "başkası" gereken testlerde ikinci bir kullanıcı eklenir.
public sealed class TaskServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    // ---------- Oluşturma ----------

    [Fact]
    public async Task CreateAsync_AddsTodoTaskToProject_AndTrimsFields()
    {
        var projectId = _db.AddProject("Proje");

        using var context = _db.CreateContext();
        var service = new TaskService(context);

        var result = await service.CreateAsync(_db.UserId, projectId, new CreateTaskRequest("  Rapor yaz  ", "  Cuma  "));

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("Rapor yaz", result.Title);
        Assert.Equal("Cuma", result.Description);
        Assert.Equal(TaskItemStatus.Todo, result.Status); // yeni görev her zaman "Yapılacak"ta başlar
        Assert.Equal(projectId, result.ProjectId);

        using var verify = _db.CreateContext();
        Assert.Equal("Rapor yaz", (await verify.Tasks.SingleAsync()).Title);
    }

    [Fact]
    public async Task CreateAsync_UnknownProject_ReturnsNullAndStoresNothing()
    {
        using var context = _db.CreateContext();
        var service = new TaskService(context);

        var result = await service.CreateAsync(_db.UserId, 999, new CreateTaskRequest("Rapor yaz", null));

        Assert.Null(result);

        using var verify = _db.CreateContext();
        Assert.Empty(await verify.Tasks.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_InProjectOfAnotherUser_ReturnsNullAndStoresNothing()
    {
        var otherUserId = _db.AddUser("baska@ornek.com");
        var theirProjectId = _db.AddProject("Başkasının projesi", ownerId: otherUserId);

        using var context = _db.CreateContext();
        var service = new TaskService(context);

        var result = await service.CreateAsync(_db.UserId, theirProjectId, new CreateTaskRequest("Sızma girişimi", null));

        Assert.Null(result);

        using var verify = _db.CreateContext();
        Assert.Empty(await verify.Tasks.ToListAsync());
    }

    // ---------- Listeleme ----------

    [Fact]
    public async Task GetByProjectAsync_ReturnsOnlyThatProjectsTasks_InCreationOrder()
    {
        var projectId = _db.AddProject("Proje");
        var otherId = _db.AddProject("Başka proje");
        _db.AddTask(projectId, "İkinci", createdAt: new DateTime(2026, 1, 2));
        _db.AddTask(projectId, "Birinci", createdAt: new DateTime(2026, 1, 1));
        _db.AddTask(otherId, "Başkasının görevi");

        using var context = _db.CreateContext();
        var service = new TaskService(context);

        var result = await service.GetByProjectAsync(_db.UserId, projectId, null);

        Assert.NotNull(result);
        Assert.Equal(new[] { "Birinci", "İkinci" }, result.Select(t => t.Title));
    }

    [Fact]
    public async Task GetByProjectAsync_WithStatusFilter_ReturnsOnlyMatchingTasks()
    {
        var projectId = _db.AddProject("Proje");
        _db.AddTask(projectId, "Yapılacak", TaskItemStatus.Todo);
        _db.AddTask(projectId, "Bitti 1", TaskItemStatus.Done);
        _db.AddTask(projectId, "Bitti 2", TaskItemStatus.Done);

        using var context = _db.CreateContext();
        var service = new TaskService(context);

        var result = await service.GetByProjectAsync(_db.UserId, projectId, TaskItemStatus.Done);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal(TaskItemStatus.Done, t.Status));
    }

    [Fact]
    public async Task GetByProjectAsync_ProjectWithoutTasks_ReturnsEmptyList_NotNull()
    {
        // null = "proje yok", boş liste = "proje var ama görevi yok". İkisi farklı anlama gelir.
        var projectId = _db.AddProject("Boş proje");

        using var context = _db.CreateContext();
        var service = new TaskService(context);

        var result = await service.GetByProjectAsync(_db.UserId, projectId, null);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByProjectAsync_UnknownProject_ReturnsNull()
    {
        using var context = _db.CreateContext();
        var service = new TaskService(context);

        var result = await service.GetByProjectAsync(_db.UserId, 999, null);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByProjectAsync_ProjectOfAnotherUser_ReturnsNull()
    {
        var otherUserId = _db.AddUser("baska@ornek.com");
        var theirProjectId = _db.AddProject("Başkasının projesi", ownerId: otherUserId);
        _db.AddTask(theirProjectId, "Başkasının görevi");

        using var context = _db.CreateContext();
        var service = new TaskService(context);

        Assert.Null(await service.GetByProjectAsync(_db.UserId, theirProjectId, null));
        Assert.NotNull(await service.GetByProjectAsync(otherUserId, theirProjectId, null));
    }

    // ---------- Tek görev ----------

    [Fact]
    public async Task GetByIdAsync_ReturnsTask()
    {
        var projectId = _db.AddProject("Proje");
        var taskId = _db.AddTask(projectId, "Rapor yaz", TaskItemStatus.InProgress);

        using var context = _db.CreateContext();
        var service = new TaskService(context);

        var result = await service.GetByIdAsync(_db.UserId, taskId);

        Assert.NotNull(result);
        Assert.Equal("Rapor yaz", result.Title);
        Assert.Equal(TaskItemStatus.InProgress, result.Status);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownTask_ReturnsNull()
    {
        using var context = _db.CreateContext();
        var service = new TaskService(context);

        Assert.Null(await service.GetByIdAsync(_db.UserId, 999));
    }

    [Fact]
    public async Task GetByIdAsync_TaskOfAnotherUser_ReturnsNull()
    {
        var otherUserId = _db.AddUser("baska@ornek.com");
        var theirProjectId = _db.AddProject("Başkasının projesi", ownerId: otherUserId);
        var theirTaskId = _db.AddTask(theirProjectId, "Başkasının görevi");

        using var context = _db.CreateContext();
        var service = new TaskService(context);

        Assert.Null(await service.GetByIdAsync(_db.UserId, theirTaskId));
        Assert.NotNull(await service.GetByIdAsync(otherUserId, theirTaskId));
    }

    // ---------- Güncelleme ----------

    [Fact]
    public async Task UpdateAsync_ChangesTitleDescriptionAndStatus()
    {
        var projectId = _db.AddProject("Proje");
        var taskId = _db.AddTask(projectId, "Eski başlık");

        using var context = _db.CreateContext();
        var service = new TaskService(context);

        var updated = await service.UpdateAsync(
            _db.UserId, taskId, new UpdateTaskRequest("  Yeni başlık  ", " Açıklama ", TaskItemStatus.Done));

        Assert.True(updated);

        using var verify = _db.CreateContext();
        var stored = await verify.Tasks.SingleAsync();
        Assert.Equal("Yeni başlık", stored.Title);
        Assert.Equal("Açıklama", stored.Description);
        Assert.Equal(TaskItemStatus.Done, stored.Status);
    }

    [Fact]
    public async Task UpdateAsync_UnknownTask_ReturnsFalse()
    {
        using var context = _db.CreateContext();
        var service = new TaskService(context);

        var updated = await service.UpdateAsync(_db.UserId, 999, new UpdateTaskRequest("Başlık", null, TaskItemStatus.Todo));

        Assert.False(updated);
    }

    [Fact]
    public async Task UpdateAsync_TaskOfAnotherUser_ReturnsFalseAndChangesNothing()
    {
        var otherUserId = _db.AddUser("baska@ornek.com");
        var theirProjectId = _db.AddProject("Başkasının projesi", ownerId: otherUserId);
        var theirTaskId = _db.AddTask(theirProjectId, "Başkasının görevi");

        using var context = _db.CreateContext();
        var service = new TaskService(context);

        var updated = await service.UpdateAsync(
            _db.UserId, theirTaskId, new UpdateTaskRequest("Ele geçirildi", null, TaskItemStatus.Done));

        Assert.False(updated);

        using var verify = _db.CreateContext();
        var stored = await verify.Tasks.SingleAsync();
        Assert.Equal("Başkasının görevi", stored.Title);
        Assert.Equal(TaskItemStatus.Todo, stored.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_ChangesOnlyTheStatus()
    {
        var projectId = _db.AddProject("Proje");
        var taskId = _db.AddTask(projectId, "Rapor yaz");

        using var context = _db.CreateContext();
        var service = new TaskService(context);

        var result = await service.UpdateStatusAsync(_db.UserId, taskId, new UpdateTaskStatusRequest(TaskItemStatus.InProgress));

        Assert.NotNull(result);
        Assert.Equal(TaskItemStatus.InProgress, result.Status);
        Assert.Equal("Rapor yaz", result.Title); // başlık değişmedi

        using var verify = _db.CreateContext();
        Assert.Equal(TaskItemStatus.InProgress, (await verify.Tasks.SingleAsync()).Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_UnknownTask_ReturnsNull()
    {
        using var context = _db.CreateContext();
        var service = new TaskService(context);

        var result = await service.UpdateStatusAsync(_db.UserId, 999, new UpdateTaskStatusRequest(TaskItemStatus.Done));

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateStatusAsync_TaskOfAnotherUser_ReturnsNullAndChangesNothing()
    {
        var otherUserId = _db.AddUser("baska@ornek.com");
        var theirProjectId = _db.AddProject("Başkasının projesi", ownerId: otherUserId);
        var theirTaskId = _db.AddTask(theirProjectId, "Başkasının görevi");

        using var context = _db.CreateContext();
        var service = new TaskService(context);

        var result = await service.UpdateStatusAsync(_db.UserId, theirTaskId, new UpdateTaskStatusRequest(TaskItemStatus.Done));

        Assert.Null(result);

        using var verify = _db.CreateContext();
        Assert.Equal(TaskItemStatus.Todo, (await verify.Tasks.SingleAsync()).Status);
    }

    // ---------- Silme ----------

    [Fact]
    public async Task DeleteAsync_RemovesOnlyThatTask()
    {
        var projectId = _db.AddProject("Proje");
        var deleteId = _db.AddTask(projectId, "Silinecek");
        _db.AddTask(projectId, "Kalacak");

        using var context = _db.CreateContext();
        var service = new TaskService(context);

        var deleted = await service.DeleteAsync(_db.UserId, deleteId);

        Assert.True(deleted);

        using var verify = _db.CreateContext();
        Assert.Equal("Kalacak", (await verify.Tasks.SingleAsync()).Title);
    }

    [Fact]
    public async Task DeleteAsync_UnknownTask_ReturnsFalse()
    {
        using var context = _db.CreateContext();
        var service = new TaskService(context);

        Assert.False(await service.DeleteAsync(_db.UserId, 999));
    }

    [Fact]
    public async Task DeleteAsync_TaskOfAnotherUser_ReturnsFalseAndKeepsTheTask()
    {
        var otherUserId = _db.AddUser("baska@ornek.com");
        var theirProjectId = _db.AddProject("Başkasının projesi", ownerId: otherUserId);
        var theirTaskId = _db.AddTask(theirProjectId, "Başkasının görevi");

        using var context = _db.CreateContext();
        var service = new TaskService(context);

        Assert.False(await service.DeleteAsync(_db.UserId, theirTaskId));

        using var verify = _db.CreateContext();
        Assert.Single(await verify.Tasks.ToListAsync());
    }
}
