using System.Net;
using System.Net.Http.Json;
using GorevTakip.Dtos;
using GorevTakip.Models;
using GorevTakip.Tests.Support;
using Xunit;

namespace GorevTakip.Tests.Api;

// Çok kullanıcılı yalıtım: Ayşe'nin verisine Mehmet hiçbir yoldan erişemez.
// Başkasının kaydı "403 yasak" değil "404 bulunamadı" döner; böylece o kaydın var olup olmadığı bile sızmaz.
public sealed class OwnershipApiTests : IDisposable
{
    private readonly GorevTakipFactory _factory = new();
    private readonly HttpClient _ayse;
    private readonly HttpClient _mehmet;

    public OwnershipApiTests()
    {
        _ayse = _factory.CreateAuthenticatedClient("ayse@ornek.com");
        _mehmet = _factory.CreateAuthenticatedClient("mehmet@ornek.com");
    }

    public void Dispose()
    {
        _ayse.Dispose();
        _mehmet.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Projects_AreListedPerUser()
    {
        await _ayse.CreateProjectAsync("Ayşe'nin projesi");
        await _mehmet.CreateProjectAsync("Mehmet'in projesi");

        var ayseList = await _ayse.GetFromJsonAsync<List<ProjectResponse>>("/projects", Json.Options);
        var mehmetList = await _mehmet.GetFromJsonAsync<List<ProjectResponse>>("/projects", Json.Options);

        Assert.Equal("Ayşe'nin projesi", Assert.Single(ayseList!).Name);
        Assert.Equal("Mehmet'in projesi", Assert.Single(mehmetList!).Name);
    }

    [Fact]
    public async Task GetProject_OfAnotherUser_Returns404()
    {
        var project = await _ayse.CreateProjectAsync("Gizli proje");

        var response = await _mehmet.GetAsync($"/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProject_OfAnotherUser_Returns404_AndChangesNothing()
    {
        var project = await _ayse.CreateProjectAsync("Gizli proje");

        var response = await _mehmet.PutAsJsonAsync(
            $"/projects/{project.Id}", new UpdateProjectRequest("Ele geçirildi", null), Json.Options);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var detail = await _ayse.GetFromJsonAsync<ProjectDetailResponse>($"/projects/{project.Id}", Json.Options);
        Assert.Equal("Gizli proje", detail!.Name);
    }

    [Fact]
    public async Task DeleteProject_OfAnotherUser_Returns404_AndKeepsTheProjectAndItsTasks()
    {
        var project = await _ayse.CreateProjectAsync("Gizli proje");
        await _ayse.CreateTaskAsync(project.Id, "Gizli görev");

        var response = await _mehmet.DeleteAsync($"/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var detail = await _ayse.GetFromJsonAsync<ProjectDetailResponse>($"/projects/{project.Id}", Json.Options);
        Assert.Single(detail!.Tasks);
    }

    [Fact]
    public async Task ListTasks_OfAnotherUsersProject_Returns404()
    {
        var project = await _ayse.CreateProjectAsync();
        await _ayse.CreateTaskAsync(project.Id, "Gizli görev");

        var response = await _mehmet.GetAsync($"/projects/{project.Id}/tasks");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_InAnotherUsersProject_Returns404_AndAddsNothing()
    {
        var project = await _ayse.CreateProjectAsync();

        var response = await _mehmet.PostAsJsonAsync(
            $"/projects/{project.Id}/tasks", new CreateTaskRequest("Sızma girişimi", null), Json.Options);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var tasks = await _ayse.GetFromJsonAsync<List<TaskResponse>>($"/projects/{project.Id}/tasks", Json.Options);
        Assert.Empty(tasks!);
    }

    [Fact]
    public async Task GetTask_OfAnotherUser_Returns404()
    {
        var project = await _ayse.CreateProjectAsync();
        var task = await _ayse.CreateTaskAsync(project.Id, "Gizli görev");

        var response = await _mehmet.GetAsync($"/tasks/{task.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_OfAnotherUser_Returns404_AndChangesNothing()
    {
        var project = await _ayse.CreateProjectAsync();
        var task = await _ayse.CreateTaskAsync(project.Id, "Gizli görev");

        var response = await _mehmet.PutAsJsonAsync(
            $"/tasks/{task.Id}", new UpdateTaskRequest("Ele geçirildi", null, TaskItemStatus.Done), Json.Options);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var stored = await _ayse.GetFromJsonAsync<TaskResponse>($"/tasks/{task.Id}", Json.Options);
        Assert.Equal("Gizli görev", stored!.Title);
        Assert.Equal(TaskItemStatus.Todo, stored.Status);
    }

    [Fact]
    public async Task MoveTask_OfAnotherUser_Returns404_AndKeepsTheStatus()
    {
        var project = await _ayse.CreateProjectAsync();
        var task = await _ayse.CreateTaskAsync(project.Id);

        var response = await _mehmet.PatchAsJsonWithEnumsAsync(
            $"/tasks/{task.Id}/status", new UpdateTaskStatusRequest(TaskItemStatus.Done));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var stored = await _ayse.GetFromJsonAsync<TaskResponse>($"/tasks/{task.Id}", Json.Options);
        Assert.Equal(TaskItemStatus.Todo, stored!.Status);
    }

    [Fact]
    public async Task DeleteTask_OfAnotherUser_Returns404_AndKeepsTheTask()
    {
        var project = await _ayse.CreateProjectAsync();
        var task = await _ayse.CreateTaskAsync(project.Id);

        var response = await _mehmet.DeleteAsync($"/tasks/{task.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _ayse.GetAsync($"/tasks/{task.Id}")).StatusCode);
    }

    [Fact]
    public async Task ProjectTaskCounts_DoNotIncludeOtherUsersData()
    {
        var project = await _ayse.CreateProjectAsync("Ayşe'nin projesi");
        await _ayse.CreateTaskAsync(project.Id, "Görev 1");
        await _ayse.CreateTaskAsync(project.Id, "Görev 2");
        await _mehmet.CreateProjectAsync("Mehmet'in projesi");

        var mehmetList = await _mehmet.GetFromJsonAsync<List<ProjectResponse>>("/projects", Json.Options);

        Assert.Equal(0, Assert.Single(mehmetList!).TaskCount);
    }
}
