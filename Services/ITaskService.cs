using GorevTakip.Dtos;
using GorevTakip.Models;

namespace GorevTakip.Services;

public interface ITaskService
{
    // null dönerse: proje bulunamadı
    Task<List<TaskResponse>?> GetByProjectAsync(int projectId, TaskItemStatus? status);

    // null dönerse: proje bulunamadı
    Task<TaskResponse?> CreateAsync(int projectId, CreateTaskRequest request);

    // null dönerse: görev bulunamadı
    Task<TaskResponse?> GetByIdAsync(int id);

    // false dönerse: görev bulunamadı
    Task<bool> UpdateAsync(int id, UpdateTaskRequest request);

    // null dönerse: görev bulunamadı
    Task<TaskResponse?> UpdateStatusAsync(int id, UpdateTaskStatusRequest request);

    // false dönerse: görev bulunamadı
    Task<bool> DeleteAsync(int id);
}
