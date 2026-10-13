using GorevTakip.Data;
using GorevTakip.Dtos;
using GorevTakip.Models;
using Microsoft.EntityFrameworkCore;

namespace GorevTakip.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;

    public AuthService(AppDbContext db, IPasswordHasher hasher, ITokenService tokens)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        var email = NormalizeEmail(request.Email);

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return null;

        var user = new User
        {
            Email = email,
            PasswordHash = _hasher.Hash(request.Password)
        };

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // İki istek aynı anda aynı e-postayla gelirse yukarıdaki kontrol ikisinden de geçer;
            // veritabanındaki benzersiz indeks ikincisini reddeder. Bunu "zaten kayıtlı" sayarız.
            if (await _db.Users.AsNoTracking().AnyAsync(u => u.Email == email))
                return null;

            throw;
        }

        return _tokens.CreateToken(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var email = NormalizeEmail(request.Email);

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            // Kullanıcı olmasa da şifre hash'leme işini yap: cevap süresi, "bu e-posta kayıtlı mı?"
            // sorusunun cevabını dışarı sızdırmasın.
            _hasher.Hash(request.Password);
            return null;
        }

        return _hasher.Verify(request.Password, user.PasswordHash)
            ? _tokens.CreateToken(user)
            : null;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
