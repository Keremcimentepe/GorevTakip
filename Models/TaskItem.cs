namespace GorevTakip.Models;

// "Task" adı System.Threading.Tasks.Task ile karışacağı için TaskItem dedik
public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Yabancı anahtar + gezinti özelliği: her görev tek bir projeye bağlı
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
}
