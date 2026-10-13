namespace GorevTakip.Services;

public interface IPasswordHasher
{
    // Şifreyi geri döndürülemez bir metne çevirir (rastgele tuz içerir: aynı şifre her seferinde farklı çıkar)
    string Hash(string password);

    // Şifre, saklanan hash ile uyuşuyor mu? Bozuk/tanınmayan biçimde false döner, hata fırlatmaz.
    bool Verify(string password, string storedHash);
}
