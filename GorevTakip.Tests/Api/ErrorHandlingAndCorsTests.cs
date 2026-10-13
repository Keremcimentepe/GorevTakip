using System.Net;
using System.Text.Json;
using GorevTakip.Dtos;
using GorevTakip.Models;
using GorevTakip.Services;
using GorevTakip.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GorevTakip.Tests.Api;

public class ErrorHandlingTests
{
    private const string SecretDetail = "SECRET-INTERNAL-DETAIL";

    [Fact]
    public async Task UnhandledException_Returns500ProblemDetails_WithoutLeakingInternals()
    {
        // ITaskService'in yerine, çağrılınca patlayan sahte bir servis takıyoruz.
        // Arayüz (interface) kullanmanın faydası tam olarak bu: endpoint değişmeden davranış değişir.
        using var factory = new GorevTakipFactory(
            services => services.AddScoped<ITaskService, ThrowingTaskService>());
        // Giriş gerekli: girişsiz istek endpoint'e ulaşmadan 401 alırdı
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/tasks/1");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SecretDetail, body); // iç ayrıntı kullanıcıya sızmamalı

        using var json = JsonDocument.Parse(body);
        Assert.Equal("Beklenmeyen bir hata oluştu.", json.RootElement.GetProperty("title").GetString());
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
    }

    // Çağrıldığında hep hata fırlatan sahte servis (test double / stub)
    private sealed class ThrowingTaskService : ITaskService
    {
        private static Exception Boom() => new InvalidOperationException(SecretDetail);

        public Task<List<TaskResponse>?> GetByProjectAsync(int userId, int projectId, TaskItemStatus? status) => throw Boom();
        public Task<TaskResponse?> CreateAsync(int userId, int projectId, CreateTaskRequest request) => throw Boom();
        public Task<TaskResponse?> GetByIdAsync(int userId, int id) => throw Boom();
        public Task<bool> UpdateAsync(int userId, int id, UpdateTaskRequest request) => throw Boom();
        public Task<TaskResponse?> UpdateStatusAsync(int userId, int id, UpdateTaskStatusRequest request) => throw Boom();
        public Task<bool> DeleteAsync(int userId, int id) => throw Boom();
    }
}

// CORS: tarayıcı, React arayüzünün (başka bir adresten) API'yi çağırmasına yalnızca
// API izin veren başlığı gönderirse izin verir. Bu başlığın doğru gelip gelmediğini sınarız.
public sealed class CorsTests : IDisposable
{
    private const string AllowedOriginHeader = "Access-Control-Allow-Origin";

    private readonly GorevTakipFactory _factory = new();
    private readonly HttpClient _client;

    public CorsTests()
    {
        _client = _factory.CreateAuthenticatedClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Theory]
    [InlineData("http://localhost:5173")] // Vite
    [InlineData("http://localhost:3000")] // Create React App
    public async Task Request_FromAllowedOrigin_GetsAllowOriginHeader(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/projects");
        request.Headers.Add("Origin", origin);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(AllowedOriginHeader, out var values));
        Assert.Equal(origin, Assert.Single(values!));
    }

    [Fact]
    public async Task Request_FromUnknownOrigin_DoesNotGetAllowOriginHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/projects");
        request.Headers.Add("Origin", "http://evil.example");

        var response = await _client.SendAsync(request);

        Assert.False(response.Headers.Contains(AllowedOriginHeader));
    }

    [Fact]
    public async Task Unauthorized_Response_StillCarriesCorsHeader()
    {
        // Pipeline sırası denetimi: CORS, kimlik doğrulamadan ÖNCE çalışmalı. Aksi halde tarayıcı
        // süresi dolmuş bir oturumda "401" yerine anlaşılmaz bir "CORS hatası" görür.
        using var anonymous = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/projects");
        request.Headers.Add("Origin", "http://localhost:5173");

        var response = await anonymous.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.Contains(AllowedOriginHeader));
    }

    [Fact]
    public async Task Preflight_WithAuthorizationHeader_IsAllowedWithoutLogin()
    {
        // Tarayıcı, Authorization başlıklı bir isteği göndermeden önce OPTIONS ile "izin var mı?" diye sorar.
        // Bu ön kontrol token taşımaz; yine de başarılı olmalı.
        using var anonymous = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/projects");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        var response = await anonymous.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains(AllowedOriginHeader));
    }
}

// Canlı ortamda izinli adres ayardan gelir (Cors__AllowedOrigins__0 ortam değişkeni).
public sealed class ConfiguredCorsTests : IDisposable
{
    private const string AllowedOriginHeader = "Access-Control-Allow-Origin";
    private const string DeployedOrigin = "https://gorevtakip.example.com";

    private readonly GorevTakipFactory _factory = new(extraSettings: new Dictionary<string, string?>
    {
        ["Cors:AllowedOrigins:0"] = DeployedOrigin
    });

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task ConfiguredOrigin_IsAllowed_AndDefaultLocalOriginsAreNot()
    {
        using var client = _factory.CreateAuthenticatedClient();

        using var deployed = new HttpRequestMessage(HttpMethod.Get, "/projects");
        deployed.Headers.Add("Origin", DeployedOrigin);
        var deployedResponse = await client.SendAsync(deployed);

        using var local = new HttpRequestMessage(HttpMethod.Get, "/projects");
        local.Headers.Add("Origin", "http://localhost:5173");
        var localResponse = await client.SendAsync(local);

        Assert.True(deployedResponse.Headers.Contains(AllowedOriginHeader));
        Assert.False(localResponse.Headers.Contains(AllowedOriginHeader));
    }
}
