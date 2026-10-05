using GorevTakip.Models;

namespace GorevTakip.Dtos;

// Entity -> DTO dönüşümleri tek yerde dursun
public static class Mappings
{
    public static TaskResponse ToResponse(this TaskItem t) =>
        new(t.Id, t.Title, t.Description, t.Status, t.CreatedAt, t.ProjectId);

    public static ProjectDetailResponse ToDetailResponse(this Project p) =>
        new(p.Id, p.Name, p.Description, p.CreatedAt,
            p.Tasks.OrderBy(t => t.CreatedAt).Select(t => t.ToResponse()).ToList());
}
