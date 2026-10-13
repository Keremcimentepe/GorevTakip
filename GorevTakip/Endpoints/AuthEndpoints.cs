using GorevTakip.Dtos;
using GorevTakip.Infrastructure;
using GorevTakip.Services;

namespace GorevTakip.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Bu uç noktalar giriş yapmadan çağrılır (token'ı buradan alırız); ama saniyede yüzlerce deneme
        // yapılıp şifre tahmin edilmesin diye hız sınırı (rate limit) var.
        var group = app.MapGroup("/auth")
            .WithTags("Auth")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth);

        group.MapPost("/register", async (RegisterRequest request, IAuthService service) =>
            {
                var result = await service.RegisterAsync(request);

                return result is null
                    ? Results.Problem(
                        title: "Bu e-posta adresiyle zaten bir hesap var.",
                        statusCode: StatusCodes.Status409Conflict)
                    : Results.Json(result, statusCode: StatusCodes.Status201Created);
            })
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>()
            .WithSummary("Yeni hesap açar ve token döner")
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        group.MapPost("/login", async (LoginRequest request, IAuthService service) =>
            {
                var result = await service.LoginAsync(request);

                // Hangisinin yanlış olduğunu söylemeyiz: e-posta mı şifre mi, saldırgan öğrenmesin
                return result is null
                    ? Results.Problem(
                        title: "E-posta veya şifre hatalı.",
                        statusCode: StatusCodes.Status401Unauthorized)
                    : Results.Ok(result);
            })
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .WithSummary("E-posta ve şifreyle giriş yapar, token döner")
            .Produces<AuthResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();
    }
}
