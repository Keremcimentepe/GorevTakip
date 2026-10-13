using GorevTakip.Dtos;
using GorevTakip.Models;

namespace GorevTakip.Services;

// Görevlerin sahibi, bağlı oldukları projenin sahibidir. Başkasının projesindeki görev,
// var olmayan görev gibi davranır.
public interface ITaskService
{
    // null dönerse: proje bulunamadı (ya da başkasının)
    Task<List<TaskResponse>?> GetByProjectAsync(int userId, int projectId, TaskItemStatus? status);

    // null dönerse: proje bulunamadı (ya da başkasının)
    Task<TaskResponse?> CreateAsync(int userId, int projectId, CreateTaskRequest request);

    // null dönerse: görev bulunamadı (ya da başkasının)
    Task<TaskResponse?> GetByIdAsync(int userId, int id);

    // false dönerse: görev bulunamadı (ya da başkasının)
    Task<bool> UpdateAsync(int userId, int id, UpdateTaskRequest request);

    // null dönerse: görev bulunamadı (ya da başkasının)
    Task<TaskResponse?> UpdateStatusAsync(int userId, int id, UpdateTaskStatusRequest request);

    // false dönerse: görev bulunamadı (ya da başkasının)
    Task<bool> DeleteAsync(int userId, int id);
}
