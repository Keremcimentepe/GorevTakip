using GorevTakip.Dtos;
using GorevTakip.Infrastructure;
using GorevTakip.Services;

namespace GorevTakip.Endpoints;

// Endpoint'ler artık "ince": isteği alır, servise devreder, sonucu HTTP cevabına çevirir.
// Veritabanı ve iş mantığı Services klasöründe.
public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/projects").WithTags("Projects");

        group.MapGet("/", async (IProjectService service) =>
                Results.Ok(await service.GetAllAsync()))
            .WithSummary("Tüm projeleri görev sayılarıyla listeler")
            .Produces<List<ProjectResponse>>();

        group.MapGet("/{id:int}", async (int id, IProjectService service) =>
            {
                var project = await service.GetByIdAsync(id);

                return project is null
                    ? Results.NotFound()
                    : Results.Ok(project);
            })
            .WithSummary("Bir projeyi görevleriyle birlikte getirir")
            .Produces<ProjectDetailResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateProjectRequest request, IProjectService service) =>
            {
                var created = await service.CreateAsync(request);

                // 201 Created + yeni kaynağın adresi (Location header)
                return Results.Created($"/projects/{created.Id}", created);
            })
            .AddEndpointFilter<ValidationFilter<CreateProjectRequest>>()
            .WithSummary("Yeni proje oluşturur")
            .Produces<ProjectResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{id:int}", async (int id, UpdateProjectRequest request, IProjectService service) =>
            {
                var updated = await service.UpdateAsync(id, request);

                return updated ? Results.NoContent() : Results.NotFound();
            })
            .AddEndpointFilter<ValidationFilter<UpdateProjectRequest>>()
            .WithSummary("Projeyi günceller")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapDelete("/{id:int}", async (int id, IProjectService service) =>
            {
                var deleted = await service.DeleteAsync(id);

                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .WithSummary("Projeyi ve görevlerini siler")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }
}
