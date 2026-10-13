using System.Net;
using System.Net.Http.Json;
using GorevTakip.Dtos;
using GorevTakip.Models;
using GorevTakip.Tests.Support;
using Xunit;

namespace GorevTakip.Tests.Api;

public sealed class TasksApiTests : IDisposable
{
    private readonly GorevTakipFactory _factory = new();
    private readonly HttpClient _client;

    public TasksApiTests()
    {
        // Girişli istemci: ilk istekte kendiliğinden hesap açar ve token ekler
        _client = _factory.CreateAuthenticatedClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ---------- POST /projects/{id}/tasks ----------

    [Fact]
    public async Task CreateTask_WithValidBody_Returns201_StartsAsTodo()
    {
        var project = await _client.CreateProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/projects/{project.Id}/tasks", new CreateTaskRequest("  Rapor yaz  ", "Cuma"), Json.Options);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TaskResponse>(Json.Options);
        Assert.NotNull(created);
        Assert.Equal("Rapor yaz", created.Title);
        Assert.Equal("Cuma", created.Description);
        Assert.Equal(TaskItemStatus.Todo, created.Status);
        Assert.Equal(project.Id, created.ProjectId);
        Assert.Equal($"/tasks/{created.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task CreateTask_InUnknownProject_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            "/projects/999/tasks", new CreateTaskRequest("Rapor yaz", null), Json.Options);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_WithBlankTitle_Returns400WithMessage()
    {
        var project = await _client.CreateProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/projects/{project.Id}/tasks", new CreateTaskRequest("   ", null), Json.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Görev başlığı boş olamaz.", await response.ReadValidationMessagesAsync());
    }

    [Fact]
    public async Task CreateTask_WithSeveralProblems_ReportsAllOfThem()
    {
        var project = await _client.CreateProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/projects/{project.Id}/tasks",
            new CreateTaskRequest(new string('a', 201), new string('b', 1001)),
            Json.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var messages = await response.ReadValidationMessagesAsync();
        Assert.Contains("Görev başlığı en fazla 200 karakter olabilir.", messages);
        Assert.Contains("Açıklama en fazla 1000 karakter olabilir.", messages);
    }

    // ---------- GET /projects/{id}/tasks ----------

    [Fact]
    public async Task GetTasks_ReturnsOnlyTasksOfThatProject()
    {
        var first = await _client.CreateProjectAsync("Birinci");
        var second = await _client.CreateProjectAsync("İkinci");
        await _client.CreateTaskAsync(first.Id, "Birincinin görevi");
        await _client.CreateTaskAsync(second.Id, "İkincinin görevi");

        var tasks = await _client.GetFromJsonAsync<List<TaskResponse>>($"/projects/{first.Id}/tasks", Json.Options);

        Assert.NotNull(tasks);
        var only = Assert.Single(tasks);
        Assert.Equal("Birincinin görevi", only.Title);
    }

    [Fact]
    public async Task GetTasks_WithStatusFilter_ReturnsOnlyMatchingTasks()
    {
        var project = await _client.CreateProjectAsync();
        await _client.CreateTaskAsync(project.Id, "Yapılacak");
        var done = await _client.CreateTaskAsync(project.Id, "Bitmiş");
        await _client.MoveTaskAsync(done.Id, TaskItemStatus.Done);

        var tasks = await _client.GetFromJsonAsync<List<TaskResponse>>(
            $"/projects/{project.Id}/tasks?status=Done", Json.Options);

        Assert.NotNull(tasks);
        var only = Assert.Single(tasks);
        Assert.Equal("Bitmiş", only.Title);
        Assert.Equal(TaskItemStatus.Done, only.Status);
    }

    [Fact]
    public async Task GetTasks_OfUnknownProject_Returns404()
    {
        var response = await _client.GetAsync("/projects/999/tasks");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- GET /tasks/{id} ----------

    [Fact]
    public async Task GetTask_ReturnsTask()
    {
        var project = await _client.CreateProjectAsync();
        var task = await _client.CreateTaskAsync(project.Id, "Rapor yaz");

        var found = await _client.GetFromJsonAsync<TaskResponse>($"/tasks/{task.Id}", Json.Options);

        Assert.NotNull(found);
        Assert.Equal("Rapor yaz", found.Title);
    }

    [Fact]
    public async Task GetTask_Unknown_Returns404()
    {
        var response = await _client.GetAsync("/tasks/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- PUT /tasks/{id} ----------

    [Fact]
    public async Task UpdateTask_Returns204AndPersistsChanges()
    {
        var project = await _client.CreateProjectAsync();
        var task = await _client.CreateTaskAsync(project.Id, "Eski başlık");

        var response = await _client.PutAsJsonAsync(
            $"/tasks/{task.Id}",
            new UpdateTaskRequest("Yeni başlık", "Yeni açıklama", TaskItemStatus.InProgress),
            Json.Options);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updated = await _client.GetFromJsonAsync<TaskResponse>($"/tasks/{task.Id}", Json.Options);
        Assert.NotNull(updated);
        Assert.Equal("Yeni başlık", updated.Title);
        Assert.Equal("Yeni açıklama", updated.Description);
        Assert.Equal(TaskItemStatus.InProgress, updated.Status);
    }

    [Fact]
    public async Task UpdateTask_Unknown_Returns404()
    {
        var response = await _client.PutAsJsonAsync(
            "/tasks/999", new UpdateTaskRequest("Başlık", null, TaskItemStatus.Todo), Json.Options);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_WithBlankTitle_Returns400()
    {
        var project = await _client.CreateProjectAsync();
        var task = await _client.CreateTaskAsync(project.Id);

        var response = await _client.PutAsJsonAsync(
            $"/tasks/{task.Id}", new UpdateTaskRequest("", null, TaskItemStatus.Todo), Json.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- PATCH /tasks/{id}/status (kartı başka kolona taşımak) ----------

    [Theory]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Done)]
    public async Task MoveTask_Returns200WithNewStatus_AndPersistsIt(TaskItemStatus target)
    {
        var project = await _client.CreateProjectAsync();
        var task = await _client.CreateTaskAsync(project.Id);

        var response = await _client.PatchAsJsonWithEnumsAsync(
            $"/tasks/{task.Id}/status", new UpdateTaskStatusRequest(target));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var moved = await response.Content.ReadFromJsonAsync<TaskResponse>(Json.Options);
        Assert.NotNull(moved);
        Assert.Equal(target, moved.Status);

        var stored = await _client.GetFromJsonAsync<TaskResponse>($"/tasks/{task.Id}", Json.Options);
        Assert.NotNull(stored);
        Assert.Equal(target, stored.Status);
    }

    [Fact]
    public async Task MoveTask_Unknown_Returns404()
    {
        var response = await _client.PatchAsJsonWithEnumsAsync(
            "/tasks/999/status", new UpdateTaskStatusRequest(TaskItemStatus.Done));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- DELETE /tasks/{id} ----------

    [Fact]
    public async Task DeleteTask_Returns204_AndTaskIsGone()
    {
        var project = await _client.CreateProjectAsync();
        var task = await _client.CreateTaskAsync(project.Id);

        var response = await _client.DeleteAsync($"/tasks/{task.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/tasks/{task.Id}")).StatusCode);
    }

    [Fact]
    public async Task DeleteTask_Unknown_Returns404()
    {
        var response = await _client.DeleteAsync("/tasks/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
