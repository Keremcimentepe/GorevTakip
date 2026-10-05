using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GorevTakip.Infrastructure;

// Hiçbir yerde yakalanmayan (beklenmeyen) hataların son durağı.
// Kullanıcıya iç detayları sızdırmadan standart bir 500 cevabı döner,
// gerçek hatayı ise log'a yazar.
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Beklenmeyen hata: {Message}", exception.Message);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Beklenmeyen bir hata oluştu.",
            Detail = "İsteğiniz işlenirken sunucuda bir sorun çıktı."
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true; // true: hata ele alındı, başka handler'a gerek yok
    }
}
