using GorevTakip.Dtos;

namespace GorevTakip.Services;

// Arayüz (interface): "ne yapılabilir" sözleşmesi. Endpoint'ler somut sınıfı değil
// bu arayüzü bilir; böylece testlerde sahte (mock) bir servis takılabilir.
public interface IProjectService
{
    Task<List<ProjectResponse>> GetAllAsync();

    // null dönerse: proje bulunamadı
    Task<ProjectDetailResponse?> GetByIdAsync(int id);

    Task<ProjectResponse> CreateAsync(CreateProjectRequest request);

    // false dönerse: proje bulunamadı
    Task<bool> UpdateAsync(int id, UpdateProjectRequest request);

    // false dönerse: proje bulunamadı
    Task<bool> DeleteAsync(int id);
}
