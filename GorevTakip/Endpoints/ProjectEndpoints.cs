using System.Security.Claims;
using GorevTakip.Dtos;
using GorevTakip.Infrastructure;
using GorevTakip.Services;

namespace GorevTakip.Endpoints;

// Endpoint'ler artık "ince": isteği alır, servise devreder, sonucu HTTP cevabına çevirir.
// Veritabanı ve iş mantığı Services klasöründe.
//
// Aşama 5: Bu gruptaki her uç nokta giriş ister (RequireAuthorization). Token yoksa ya da
// geçersizse istek endpoint'e hiç ulaşmadan 401 ile döner. `ClaimsPrincipal user` parametresi,
// token'dan çıkarılan kullanıcıdır; framework otomatik doldurur.
public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/projects").WithTags("Projects").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, IProjectService service) =>
                Results.Ok(await service.GetAllAsync(user.GetUserId())))
            .WithSummary("Giriş yapan kullanıcının projelerini görev sayılarıyla listeler")
            .Produces<List<ProjectResponse>>();

        group.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, IProjectService service) =>
            {
                var project = await service.GetByIdAsync(user.GetUserId(), id);

                return project is null
                    ? Results.NotFound()
                    : Results.Ok(project);
            })
            .WithSummary("Bir projeyi görevleriyle birlikte getirir")
            .Produces<ProjectDetailResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateProjectRequest request, ClaimsPrincipal user, IProjectService service) =>
            {
                var created = await service.CreateAsync(user.GetUserId(), request);

                // 201 Created + yeni kaynağın adresi (Location header)
                return Results.Created($"/projects/{created.Id}", created);
            })
            .AddEndpointFilter<ValidationFilter<CreateProjectRequest>>()
            .WithSummary("Yeni proje oluşturur")
            .Produces<ProjectResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{id:int}", async (int id, UpdateProjectRequest request, ClaimsPrincipal user, IProjectService service) =>
            {
                var updated = await service.UpdateAsync(user.GetUserId(), id, request);

                return updated ? Results.NoContent() : Results.NotFound();
            })
            .AddEndpointFilter<ValidationFilter<UpdateProjectRequest>>()
            .WithSummary("Projeyi günceller")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, IProjectService service) =>
            {
                var deleted = await service.DeleteAsync(user.GetUserId(), id);

                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .WithSummary("Projeyi ve görevlerini siler")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }
}
