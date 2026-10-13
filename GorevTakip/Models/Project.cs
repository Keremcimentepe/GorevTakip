namespace GorevTakip.Models;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Projenin sahibi. Her sorgu bu alana göre süzülür: kullanıcılar yalnızca kendi projelerini görür.
    public int OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    // Bir projenin birden çok görevi olur (bire-çok ilişki)
    public List<TaskItem> Tasks { get; set; } = new();
}
