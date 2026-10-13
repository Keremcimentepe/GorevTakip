using GorevTakip.Data;
using GorevTakip.Dtos;
using GorevTakip.Models;
using Microsoft.EntityFrameworkCore;

namespace GorevTakip.Services;

public class TaskService : ITaskService
{
    private readonly AppDbContext _db;

    public TaskService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<TaskResponse>?> GetByProjectAsync(int userId, int projectId, TaskItemStatus? status)
    {
        if (!await OwnsProjectAsync(userId, projectId))
            return null;

        var query = _db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId);

        if (status is not null)
            query = query.Where(t => t.Status == status);

        var tasks = await query.OrderBy(t => t.CreatedAt).ToListAsync();

        return tasks.Select(t => t.ToResponse()).ToList();
    }

    public async Task<TaskResponse?> CreateAsync(int userId, int projectId, CreateTaskRequest request)
    {
        if (!await OwnsProjectAsync(userId, projectId))
            return null;

        var task = new TaskItem
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            ProjectId = projectId
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();

        return task.ToResponse();
    }

    public async Task<TaskResponse?> GetByIdAsync(int userId, int id)
    {
        var task = await _db.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.Project.OwnerId == userId);

        return task?.ToResponse();
    }

    public async Task<bool> UpdateAsync(int userId, int id, UpdateTaskRequest request)
    {
        var task = await FindOwnedAsync(userId, id);
        if (task is null)
            return false;

        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.Status = request.Status;
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<TaskResponse?> UpdateStatusAsync(int userId, int id, UpdateTaskStatusRequest request)
    {
        var task = await FindOwnedAsync(userId, id);
        if (task is null)
            return null;

        task.Status = request.Status;
        await _db.SaveChangesAsync();

        return task.ToResponse();
    }

    public async Task<bool> DeleteAsync(int userId, int id)
    {
        var task = await FindOwnedAsync(userId, id);
        if (task is null)
            return false;

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync();

        return true;
    }

    private Task<bool> OwnsProjectAsync(int userId, int projectId) =>
        _db.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == userId);

    // Görevin sahibi = bağlı olduğu projenin sahibi
    private Task<TaskItem?> FindOwnedAsync(int userId, int id) =>
        _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.Project.OwnerId == userId);
}
