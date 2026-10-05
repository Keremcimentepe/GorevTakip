namespace GorevTakip.Models;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Bir projenin birden çok görevi olur (bire-çok ilişki)
    public List<TaskItem> Tasks { get; set; } = new();
}
