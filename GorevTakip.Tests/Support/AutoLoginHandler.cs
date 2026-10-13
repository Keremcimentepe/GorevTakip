using System.Net.Http.Headers;
using System.Net.Http.Json;
using GorevTakip.Dtos;

namespace GorevTakip.Tests.Support;

/// <summary>
/// HttpClient'ın isteklerinin arasına giren küçük bir ara katman. İlk istekten önce /auth/register ile
/// hesap açıp token alır, sonra her isteğe "Authorization: Bearer ..." başlığını ekler.
/// Testin kendisi hiç giriş kodu yazmak zorunda kalmaz.
/// </summary>
internal sealed class AutoLoginHandler : DelegatingHandler
{
    private readonly string _email;
    private readonly string _password;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;

    public AutoLoginHandler(string email, string password)
    {
        _email = email;
        _password = password;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null)
            return _token;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null)
                return _token;

            // Adres önemsiz (TestServer'a gider); yalnızca yol kullanılır
            using var registration = new HttpRequestMessage(HttpMethod.Post, "http://localhost/auth/register")
            {
                Content = JsonContent.Create(new RegisterRequest(_email, _password))
            };

            using var response = await base.SendAsync(registration, cancellationToken);
            response.EnsureSuccessStatusCode();

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json.Options, cancellationToken);
            _token = auth!.Token;

            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }
}
