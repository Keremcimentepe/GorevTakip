using GorevTakip.Data;
using GorevTakip.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GorevTakip.Tests.Support;

/// <summary>
/// Servis testleri için temiz bir bellek içi SQLite veritabanı.
/// (EF'in "InMemory" sağlayıcısı yerine gerçek SQLite kullanıyoruz: yabancı anahtar,
/// cascade silme ve enum'un metin olarak saklanması gerçekteki gibi çalışsın.)
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDb()
    {
        // ":memory:" veritabanı bağlantıyla birlikte yaşar ve ölür; bu yüzden bağlantıyı
        // açık tutuyor ve tüm DbContext'lere aynı bağlantıyı veriyoruz.
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();

        UserId = AddUser("varsayilan@ornek.com");
    }

    /// <summary>
    /// Varsayılan kullanıcı. Projeler (başka biri belirtilmedikçe) bu kullanıcıya ait olur;
    /// servis testleri de çoğunlukla bu kimlikle çağrılır.
    /// </summary>
    public int UserId { get; }

    /// <summary>
    /// Her çağrıda yeni bir DbContext verir. Hazırlık (arrange), işlem (act) ve doğrulama (assert)
    /// için ayrı context kullanmak, EF'in önbelleğindeki nesnelerin testi yanıltmasını önler.
    /// </summary>
    public AppDbContext CreateContext() => new(_options);

    public int AddUser(string email)
    {
        using var context = CreateContext();
        // PasswordHash'in gerçek bir hash olması gerekmez; bu testler giriş yapmıyor
        var user = new User { Email = email, PasswordHash = "test-hash" };
        context.Users.Add(user);
        context.SaveChanges();
        return user.Id;
    }

    public int AddProject(string name, DateTime? createdAt = null, int? ownerId = null)
    {
        using var context = CreateContext();
        var project = new Project
        {
            Name = name,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            OwnerId = ownerId ?? UserId
        };
        context.Projects.Add(project);
        context.SaveChanges();
        return project.Id;
    }

    public int AddTask(
        int projectId,
        string title,
        TaskItemStatus status = TaskItemStatus.Todo,
        DateTime? createdAt = null)
    {
        using var context = CreateContext();
        var task = new TaskItem
        {
            Title = title,
            Status = status,
            ProjectId = projectId,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        context.Tasks.Add(task);
        context.SaveChanges();
        return task.Id;
    }

    public void Dispose() => _connection.Dispose();
}
