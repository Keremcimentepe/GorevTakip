using System.Security.Claims;
using GorevTakip.Dtos;
using GorevTakip.Infrastructure;
using GorevTakip.Models;
using GorevTakip.Services;

namespace GorevTakip.Endpoints;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        // --- Bir projenin altındaki görevler: listele ve ekle ---
        var projectTasks = app.MapGroup("/projects/{projectId:int}/tasks").WithTags("Tasks").RequireAuthorization();

        // İsteğe bağlı filtre: GET /projects/1/tasks?status=Done
        projectTasks.MapGet("/", async (int projectId, TaskItemStatus? status, ClaimsPrincipal user, ITaskService service) =>
            {
                var tasks = await service.GetByProjectAsync(user.GetUserId(), projectId, status);

                return tasks is null
                    ? Results.NotFound()
                    : Results.Ok(tasks);
            })
            .WithSummary("Projenin görevlerini listeler (durum filtresi opsiyonel)")
            .Produces<List<TaskResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        projectTasks.MapPost("/", async (int projectId, CreateTaskRequest request, ClaimsPrincipal user, ITaskService service) =>
            {
                var created = await service.CreateAsync(user.GetUserId(), projectId, request);

                return created is null
                    ? Results.NotFound()
                    : Results.Created($"/tasks/{created.Id}", created);
            })
            .AddEndpointFilter<ValidationFilter<CreateTaskRequest>>()
            .WithSummary("Projeye yeni görev ekler")
            .Produces<TaskResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        // --- Tek bir görev üzerinde işlemler ---
        var tasks = app.MapGroup("/tasks").WithTags("Tasks").RequireAuthorization();

        tasks.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, ITaskService service) =>
            {
                var task = await service.GetByIdAsync(user.GetUserId(), id);

                return task is null
                    ? Results.NotFound()
                    : Results.Ok(task);
            })
            .WithSummary("Tek bir görevi getirir")
            .Produces<TaskResponse>()
            .Produces(StatusCodes.Status404NotFound);

        tasks.MapPut("/{id:int}", async (int id, UpdateTaskRequest request, ClaimsPrincipal user, ITaskService service) =>
            {
                var updated = await service.UpdateAsync(user.GetUserId(), id, request);

                return updated ? Results.NoContent() : Results.NotFound();
            })
            .AddEndpointFilter<ValidationFilter<UpdateTaskRequest>>()
            .WithSummary("Görevi günceller")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        // Kartı başka kolona taşımak: sadece durumu değiştirir
        tasks.MapPatch("/{id:int}/status", async (int id, UpdateTaskStatusRequest request, ClaimsPrincipal user, ITaskService service) =>
            {
                var task = await service.UpdateStatusAsync(user.GetUserId(), id, request);

                return task is null
                    ? Results.NotFound()
                    : Results.Ok(task);
            })
            .AddEndpointFilter<ValidationFilter<UpdateTaskStatusRequest>>()
            .WithSummary("Görevin durumunu değiştirir")
            .Produces<TaskResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        tasks.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, ITaskService service) =>
            {
                var deleted = await service.DeleteAsync(user.GetUserId(), id);

                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .WithSummary("Görevi siler")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }
}
