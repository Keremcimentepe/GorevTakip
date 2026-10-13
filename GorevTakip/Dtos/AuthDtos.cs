namespace GorevTakip.Dtos;

public record RegisterRequest(string Email, string Password);

public record LoginRequest(string Email, string Password);

// Giriş/kayıt başarılı olunca dönen cevap. Arayüz token'ı saklar ve sonraki isteklere ekler.
// ExpiresAt (UTC): token'ın bitiş zamanı.
public record AuthResponse(string Token, DateTime ExpiresAt, string Email);
