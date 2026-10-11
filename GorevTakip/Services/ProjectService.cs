using GorevTakip.Data;
using GorevTakip.Dtos;
using GorevTakip.Models;
using Microsoft.EntityFrameworkCore;

namespace GorevTakip.Services;

public class ProjectService : IProjectService
{
    private readonly AppDbContext _db;

    // Dependency injection: AppDbContext'i biz oluşturmuyoruz, framework veriyor
    public ProjectService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ProjectResponse>> GetAllAsync()
    {
        return await _db.Projects
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProjectResponse(
                p.Id, p.Name, p.Description, p.CreatedAt, p.Tasks.Count))
            .ToListAsync();
    }

    public async Task<ProjectDetailResponse?> GetByIdAsync(int id)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id);

        return project?.ToDetailResponse();
    }

    public async Task<ProjectResponse> CreateAsync(CreateProjectRequest request)
    {
        var project = new Project
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim()
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        return new ProjectResponse(project.Id, project.Name, project.Description, project.CreatedAt, 0);
    }

    public async Task<bool> UpdateAsync(int id, UpdateProjectRequest request)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project is null)
            return false;

        project.Name = request.Name.Trim();
        project.Description = request.Description?.Trim();
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project is null)
            return false;

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();

        return true;
    }
}
