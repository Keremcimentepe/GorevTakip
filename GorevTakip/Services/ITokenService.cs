using GorevTakip.Dtos;
using GorevTakip.Models;

namespace GorevTakip.Services;

public interface ITokenService
{
    // Kullanıcı için imzalı bir JWT üretir ve istemciye dönecek cevabı hazırlar
    AuthResponse CreateToken(User user);
}
