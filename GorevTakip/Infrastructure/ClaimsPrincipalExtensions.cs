using System.Globalization;
using System.Security.Claims;

namespace GorevTakip.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Token'ın "sub" (subject) talebinden giriş yapmış kullanıcının kimliğini okur.
    /// Endpoint'ler bu kimliği servislere verir; servisler yalnızca o kullanıcının verisini görür.
    /// </summary>
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        // Talep adı, JWT ayarına göre "sub" ya da ClaimTypes.NameIdentifier olarak gelebilir
        var value = principal.FindFirst("sub")?.Value
                    ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            throw new InvalidOperationException("Kimliği doğrulanmış kullanıcının geçerli bir 'sub' talebi yok.");

        return id;
    }
}
