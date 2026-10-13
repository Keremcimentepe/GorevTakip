using GorevTakip.Models;
using Microsoft.EntityFrameworkCore;

namespace GorevTakip.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            // 254: e-posta adresinin standart üst sınırı
            entity.Property(u => u.Email).IsRequired().HasMaxLength(254);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(200);

            // Aynı e-postayla iki hesap açılamasın; kontrol uygulamada değil veritabanında da dursun
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.Property(p => p.Name).IsRequired().HasMaxLength(100);
            entity.Property(p => p.Description).HasMaxLength(500);

            // Kullanıcı silinirse projeleri (ve onların görevleri) de silinsin
            entity.HasOne(p => p.Owner)
                  .WithMany(u => u.Projects)
                  .HasForeignKey(p => p.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.Property(t => t.Title).IsRequired().HasMaxLength(200);
            entity.Property(t => t.Description).HasMaxLength(1000);

            // Veritabanında 0/1/2 yerine "Todo"/"Done" gibi metin saklansın
            entity.Property(t => t.Status).HasConversion<string>();

            // Proje silinince görevleri de silinsin
            entity.HasOne(t => t.Project)
                  .WithMany(p => p.Tasks)
                  .HasForeignKey(t => t.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
