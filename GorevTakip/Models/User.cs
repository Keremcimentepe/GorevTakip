namespace GorevTakip.Models;

public class User
{
    public int Id { get; set; }

    // Küçük harfe çevrilmiş halde saklanır ("Ali@Mail.com" ile "ali@mail.com" aynı hesaptır).
    // Veritabanında benzersiz indeks vardır.
    public string Email { get; set; } = string.Empty;

    // Şifrenin kendisi ASLA saklanmaz; yalnızca tuzlu (salted) hash'i saklanır.
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Bir kullanıcının birden çok projesi olur (bire-çok ilişki)
    public List<Project> Projects { get; set; } = new();
}
