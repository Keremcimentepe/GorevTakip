using System.Net;
using System.Net.Http.Json;
using GorevTakip.Dtos;
using GorevTakip.Tests.Support;
using Xunit;

namespace GorevTakip.Tests.Api;

// Entegrasyon testi: gerçek HTTP isteği → gerçek pipeline (doğrulama filtresi, servis, EF Core,
// SQLite) → gerçek JSON cevabı. Sadece sunucu bellekte çalışır, ağa çıkılmaz.
// Aşama 5: Bu uç noktalar giriş ister; _client ilk istekte kendiliğinden hesap açıp token ekler.
// (Girişsiz erişim ve başkasının verisi AuthApiTests ile OwnershipApiTests'te sınanır.)
public sealed class ProjectsApiTests : IDisposable
{
    private readonly GorevTakipFactory _factory = new();
    private readonly HttpClient _client;

    public ProjectsApiTests()
    {
        _client = _factory.CreateAuthenticatedClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ---------- POST /projects ----------

    [Fact]
    public async Task CreateProject_WithValidBody_Returns201WithLocation()
    {
        var response = await _client.PostAsJsonAsync(
            "/projects", new CreateProjectRequest("  Web sitesi  ", "  Kurumsal site  "), Json.Options);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<ProjectResponse>(Json.Options);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal("Web sitesi", created.Name); // baştaki/sondaki boşluklar temizlenir
        Assert.Equal("Kurumsal site", created.Description);
        Assert.Equal(0, created.TaskCount);
        Assert.Equal($"/projects/{created.Id}", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateProject_WithBlankName_Returns400WithMessage(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/projects", new CreateProjectRequest(name, null), Json.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Proje adı boş olamaz.", await response.ReadValidationMessagesAsync());
    }

    [Fact]
    public async Task CreateProject_WithTooLongName_Returns400()
    {
        var response = await _client.PostAsJsonAsync(
            "/projects", new CreateProjectRequest(new string('a', 101), null), Json.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Proje adı en fazla 100 karakter olabilir.", await response.ReadValidationMessagesAsync());
    }

    [Fact]
    public async Task CreateProject_WhenInvalid_StoresNothing()
    {
        await _client.PostAsJsonAsync("/projects", new CreateProjectRequest("", null), Json.Options);

        var projects = await _client.GetFromJsonAsync<List<ProjectResponse>>("/projects", Json.Options);

        Assert.NotNull(projects);
        Assert.Empty(projects);
    }

    // ---------- GET /projects ----------

    [Fact]
    public async Task GetProjects_WhenEmpty_ReturnsEmptyArray()
    {
        var response = await _client.GetAsync("/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var projects = await response.Content.ReadFromJsonAsync<List<ProjectResponse>>(Json.Options);
        Assert.NotNull(projects);
        Assert.Empty(projects);
    }

    [Fact]
    public async Task GetProjects_ReturnsProjectsWithTaskCounts()
    {
        var project = await _client.CreateProjectAsync("Sayaç testi");
        await _client.CreateTaskAsync(project.Id, "Birinci");
        await _client.CreateTaskAsync(project.Id, "İkinci");

        var projects = await _client.GetFromJsonAsync<List<ProjectResponse>>("/projects", Json.Options);

        Assert.NotNull(projects);
        var found = Assert.Single(projects);
        Assert.Equal("Sayaç testi", found.Name);
        Assert.Equal(2, found.TaskCount);
    }

    // ---------- GET /projects/{id} ----------

    [Fact]
    public async Task GetProject_ReturnsProjectWithItsTasks()
    {
        var project = await _client.CreateProjectAsync("Detay testi", "Açıklama");
        await _client.CreateTaskAsync(project.Id, "Görev A");
        await _client.CreateTaskAsync(project.Id, "Görev B");

        var detail = await _client.GetFromJsonAsync<ProjectDetailResponse>($"/projects/{project.Id}", Json.Options);

        Assert.NotNull(detail);
        Assert.Equal("Detay testi", detail.Name);
        Assert.Equal("Açıklama", detail.Description);
        Assert.Equal(2, detail.Tasks.Count);
    }

    [Fact]
    public async Task GetProject_Unknown_Returns404()
    {
        var response = await _client.GetAsync("/projects/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- PUT /projects/{id} ----------

    [Fact]
    public async Task UpdateProject_WithValidBody_Returns204AndPersistsChange()
    {
        var project = await _client.CreateProjectAsync("Eski ad");

        var response = await _client.PutAsJsonAsync(
            $"/projects/{project.Id}", new UpdateProjectRequest("  Yeni ad  ", "Yeni açıklama"), Json.Options);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var detail = await _client.GetFromJsonAsync<ProjectDetailResponse>($"/projects/{project.Id}", Json.Options);
        Assert.NotNull(detail);
        Assert.Equal("Yeni ad", detail.Name);
        Assert.Equal("Yeni açıklama", detail.Description);
    }

    [Fact]
    public async Task UpdateProject_Unknown_Returns404()
    {
        var response = await _client.PutAsJsonAsync(
            "/projects/999", new UpdateProjectRequest("Ad", null), Json.Options);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProject_WithBlankName_Returns400_EvenIfProjectDoesNotExist()
    {
        // Doğrulama filtresi endpoint'ten ÖNCE çalışır: önce istek gövdesi, sonra kayıt kontrol edilir.
        var response = await _client.PutAsJsonAsync(
            "/projects/999", new UpdateProjectRequest("", null), Json.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- DELETE /projects/{id} ----------

    [Fact]
    public async Task DeleteProject_Returns204_AndProjectIsGone()
    {
        var project = await _client.CreateProjectAsync("Silinecek");

        var response = await _client.DeleteAsync($"/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/projects/{project.Id}")).StatusCode);
    }

    [Fact]
    public async Task DeleteProject_AlsoDeletesItsTasks()
    {
        var project = await _client.CreateProjectAsync("Silinecek");
        var task = await _client.CreateTaskAsync(project.Id, "Projeyle birlikte gitmeli");

        await _client.DeleteAsync($"/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/tasks/{task.Id}")).StatusCode);
    }

    [Fact]
    public async Task DeleteProject_Unknown_Returns404()
    {
        var response = await _client.DeleteAsync("/projects/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
