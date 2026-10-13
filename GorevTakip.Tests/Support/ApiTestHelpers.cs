using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GorevTakip.Dtos;
using GorevTakip.Models;

namespace GorevTakip.Tests.Support;

public static class Json
{
    /// <summary>
    /// API enum'ları metin olarak gönderir ("Done"). İstemci tarafında da aynı ayarı kullanmazsak
    /// ReadFromJsonAsync metni enum'a çeviremez.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

public static class HttpClientTestExtensions
{
    public static Task<HttpResponseMessage> PatchAsJsonWithEnumsAsync<T>(
        this HttpClient client, string url, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = JsonContent.Create(body, options: Json.Options)
        };
        return client.SendAsync(request);
    }

    /// <summary>Hazırlık adımı: bir proje oluşturur, başarısızsa testi hemen patlatır.</summary>
    public static async Task<ProjectResponse> CreateProjectAsync(
        this HttpClient client, string name = "Test projesi", string? description = null)
    {
        var response = await client.PostAsJsonAsync(
            "/projects", new CreateProjectRequest(name, description), Json.Options);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(Json.Options))!;
    }

    /// <summary>Hazırlık adımı: projeye görev ekler.</summary>
    public static async Task<TaskResponse> CreateTaskAsync(
        this HttpClient client, int projectId, string title = "Test görevi", string? description = null)
    {
        var response = await client.PostAsJsonAsync(
            $"/projects/{projectId}/tasks", new CreateTaskRequest(title, description), Json.Options);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TaskResponse>(Json.Options))!;
    }

    public static async Task<TaskResponse> MoveTaskAsync(
        this HttpClient client, int taskId, TaskItemStatus status)
    {
        var response = await client.PatchAsJsonWithEnumsAsync(
            $"/tasks/{taskId}/status", new UpdateTaskStatusRequest(status));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TaskResponse>(Json.Options))!;
    }

    /// <summary>
    /// 400 cevabındaki ("errors": { "Alan": ["mesaj", ...] }) bütün mesajları düz bir listeye çevirir.
    /// Ham metinde arama yapmak yerine JSON'u ayrıştırıyoruz; çünkü System.Text.Json varsayılan olarak
    /// Türkçe harfleri ı gibi kaçış dizilerine çevirir ve metin eşleşmesi tutmaz.
    /// </summary>
    public static async Task<List<string>> ReadValidationMessagesAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var messages = new List<string>();

        if (document.RootElement.TryGetProperty("errors", out var errors))
        {
            foreach (var field in errors.EnumerateObject())
            {
                foreach (var message in field.Value.EnumerateArray())
                    messages.Add(message.GetString()!);
            }
        }

        return messages;
    }
}
