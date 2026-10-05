using GorevTakip.Models;

namespace GorevTakip.Dtos;

public record CreateTaskRequest(string Title, string? Description);

public record UpdateTaskRequest(string Title, string? Description, TaskItemStatus Status);

// Sadece durumu değiştirmek için (kartı bir kolondan diğerine sürüklemek gibi)
public record UpdateTaskStatusRequest(TaskItemStatus Status);

public record TaskResponse(
    int Id, string Title, string? Description, TaskItemStatus Status, DateTime CreatedAt, int ProjectId);
