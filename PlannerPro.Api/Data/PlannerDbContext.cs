using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlannerPro.Api.Domain;

namespace PlannerPro.Api.Data;

/// <summary>EF Core context for PlannerPro. Extends IdentityDbContext so the
/// single-user auth tables live in the same database.</summary>
public class PlannerDbContext(DbContextOptions<PlannerDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<SprintGoal> SprintGoals => Set<SprintGoal>();
    public DbSet<PlannerTask> Tasks => Set<PlannerTask>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Project>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
            e.Property(p => p.Slug).HasMaxLength(50).IsRequired();
            e.HasIndex(p => p.Slug).IsUnique();
            e.Property(p => p.ColorHex).HasMaxLength(9).IsRequired();
        });

        b.Entity<Sprint>(e =>
        {
            e.HasIndex(s => s.Number).IsUnique();
        });

        b.Entity<SprintGoal>(e =>
        {
            e.Property(g => g.GoalText).HasMaxLength(300).IsRequired();
            e.Property(g => g.Status).HasConversion<int>();

            // Enforces "exactly one goal per project per sprint".
            e.HasIndex(g => new { g.SprintId, g.ProjectId }).IsUnique();

            e.HasOne(g => g.Sprint)
                .WithMany(s => s.Goals)
                .HasForeignKey(g => g.SprintId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict (not cascade) to avoid multiple cascade paths into
            // SprintGoal on SQL Server. Projects are fixed and never deleted.
            e.HasOne(g => g.Project)
                .WithMany(p => p.Goals)
                .HasForeignKey(g => g.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PlannerTask>(e =>
        {
            e.ToTable("Tasks");
            e.Property(t => t.Label).HasMaxLength(300).IsRequired();
            // Stored as the underlying Fibonacci value so effort = SUM(Points).
            e.Property(t => t.Points).HasConversion<int>();
            e.Property(t => t.Priority).HasConversion<int>();

            e.HasOne(t => t.SprintGoal)
                .WithMany(g => g.Tasks)
                .HasForeignKey(t => t.SprintGoalId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
