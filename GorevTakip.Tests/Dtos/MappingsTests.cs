using GorevTakip.Dtos;
using GorevTakip.Models;
using Xunit;

namespace GorevTakip.Tests.Dtos;

public class MappingsTests
{
    [Fact]
    public void ToResponse_CopiesAllFields()
    {
        var createdAt = new DateTime(2026, 3, 1, 10, 30, 0, DateTimeKind.Utc);
        var task = new TaskItem
        {
            Id = 7,
            Title = "Rapor yaz",
            Description = "Cuma gününe kadar",
            Status = TaskItemStatus.InProgress,
            CreatedAt = createdAt,
            ProjectId = 3
        };

        var response = task.ToResponse();

        Assert.Equal(new TaskResponse(7, "Rapor yaz", "Cuma gününe kadar", TaskItemStatus.InProgress, createdAt, 3), response);
    }

    [Fact]
    public void ToDetailResponse_OrdersTasksByCreationTime()
    {
        var project = new Project
        {
            Id = 1,
            Name = "Proje",
            Tasks = new List<TaskItem>
            {
                new() { Id = 1, Title = "Sonra", CreatedAt = new DateTime(2026, 1, 2) },
                new() { Id = 2, Title = "Önce", CreatedAt = new DateTime(2026, 1, 1) },
                new() { Id = 3, Title = "En son", CreatedAt = new DateTime(2026, 1, 3) }
            }
        };

        var detail = project.ToDetailResponse();

        Assert.Equal(new[] { "Önce", "Sonra", "En son" }, detail.Tasks.Select(t => t.Title));
    }
}
