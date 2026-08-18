using DevGuard.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DevGuard.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<RegisteredProject> Projects => Set<RegisteredProject>();
    public DbSet<CodeIssue> Issues => Set<CodeIssue>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ScanHistory> ScanHistories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RegisteredProject>()
            .HasMany(p => p.Issues)
            .WithOne()
            .HasForeignKey(i => i.RegisteredProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AppSetting>()
            .HasKey(s => s.Key);
    }
}