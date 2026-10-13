using GorevTakip.Dtos;

namespace GorevTakip.Services;

public interface IAuthService
{
    // null dönerse: bu e-posta zaten kayıtlı
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);

    // null dönerse: e-posta ya da şifre yanlış (hangisi olduğu bilerek söylenmez)
    Task<AuthResponse?> LoginAsync(LoginRequest request);
}
