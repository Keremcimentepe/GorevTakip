using GorevTakip.Dtos;

namespace GorevTakip.Services;

// Arayüz (interface): "ne yapılabilir" sözleşmesi. Endpoint'ler somut sınıfı değil
// bu arayüzü bilir; böylece testlerde sahte (mock) bir servis takılabilir.
//
// Aşama 5: Her metot "kim istiyor?" bilgisini (userId) alır ve yalnızca o kullanıcının projelerine
// dokunur. Başkasının projesi, var olmayan proje gibi davranır ("bulunamadı"); böylece başkasının
// kaydının var olup olmadığı bile sızmaz.
public interface IProjectService
{
    Task<List<ProjectResponse>> GetAllAsync(int userId);

    // null dönerse: proje bulunamadı (ya da başkasının)
    Task<ProjectDetailResponse?> GetByIdAsync(int userId, int id);

    Task<ProjectResponse> CreateAsync(int userId, CreateProjectRequest request);

    // false dönerse: proje bulunamadı (ya da başkasının)
    Task<bool> UpdateAsync(int userId, int id, UpdateProjectRequest request);

    // false dönerse: proje bulunamadı (ya da başkasının)
    Task<bool> DeleteAsync(int userId, int id);
}
