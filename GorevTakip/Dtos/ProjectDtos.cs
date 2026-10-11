namespace GorevTakip.Dtos;

// DTO: API'nin dışarıya açtığı veri şekli. Veritabanı modelini (entity) doğrudan
// dışarı vermiyoruz; böylece modeli değiştirmek API'yi bozmuyor.
public record CreateProjectRequest(string Name, string? Description);

public record UpdateProjectRequest(string Name, string? Description);

public record ProjectResponse(int Id, string Name, string? Description, DateTime CreatedAt, int TaskCount);

public record ProjectDetailResponse(
    int Id, string Name, string? Description, DateTime CreatedAt, List<TaskResponse> Tasks);
