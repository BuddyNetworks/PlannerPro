using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlannerPro.Api.Domain;

namespace PlannerPro.Api.Data;

/// <summary>Idempotent database seeder: three real projects, the full sprint
/// calendar, placeholder goals/tasks for the first few sprints, and the single
/// login user (from config/user-secrets). Safe to run on every startup.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var db = services.GetRequiredService<PlannerDbContext>();

        await SeedProjectsAsync(db);
        await SeedSprintsAsync(db);
        await SeedPlaceholderGoalsAsync(db, logger);
        await SeedSingleUserAsync(services, config, logger);
    }

    private static async Task SeedProjectsAsync(PlannerDbContext db)
    {
        if (await db.Projects.AnyAsync()) return;

        db.Projects.AddRange(
            new Project { Name = "BuddyNetworks", Slug = "buddynetworks", ColorHex = "#c2410c", SortOrder = 0 },
            new Project { Name = "BawdyRooster", Slug = "bawdyrooster", ColorHex = "#7c3aed", SortOrder = 1 },
            new Project { Name = "Roosters", Slug = "roosters", ColorHex = "#0891b2", SortOrder = 2 });

        await db.SaveChangesAsync();
    }

    private static async Task SeedSprintsAsync(PlannerDbContext db)
    {
        if (await db.Sprints.AnyAsync()) return;

        db.Sprints.AddRange(SprintCalendar.GenerateThroughEndOf2027());
        await db.SaveChangesAsync();
    }

    private static async Task SeedPlaceholderGoalsAsync(PlannerDbContext db, ILogger logger)
    {
        if (await db.SprintGoals.AnyAsync()) return;

        var projects = await db.Projects.OrderBy(p => p.SortOrder).ToListAsync();
        var sprints = await db.Sprints.OrderBy(s => s.Number).Take(3).ToListAsync();
        if (projects.Count < 3 || sprints.Count < 3) return;

        var bySlug = projects.ToDictionary(p => p.Slug);
        var now = DateTimeOffset.UtcNow;

        void AddGoal(string slug, int sprintIdx, string goalText, GoalStatus status,
            (string label, EffortPoints pts, TaskPriority? prio, bool done)[] tasks)
        {
            var goal = new SprintGoal
            {
                SprintId = sprints[sprintIdx].Id,
                ProjectId = bySlug[slug].Id,
                GoalText = goalText,
                Status = status,
            };
            var order = 0;
            foreach (var (label, pts, prio, done) in tasks)
            {
                goal.Tasks.Add(new PlannerTask
                {
                    Label = label,
                    Points = pts,
                    Priority = prio,
                    IsDone = done,
                    CompletedAt = done ? now : null,
                    SortOrder = order++,
                    CreatedAt = now,
                });
            }
            db.SprintGoals.Add(goal);
        }

        var H = (TaskPriority?)TaskPriority.High;
        var M = (TaskPriority?)TaskPriority.Medium;
        var _ = (TaskPriority?)null;

        // Sprint 1 — healthy (~22 pts total): launch wind-down + kickoffs.
        AddGoal("buddynetworks", 0, "Finish final QA pass across all six sites", GoalStatus.InProgress, new[]
        {
            ("Regression sweep: signup + billing on all 6 domains", EffortPoints.P5, H, true),
            ("Verify uptime + error alerting wired for each site", EffortPoints.P3, M, false),
        });
        AddGoal("bawdyrooster", 0, "Stand up project skeleton and core domain model", GoalStatus.InProgress, new[]
        {
            ("Scaffold solution + CI", EffortPoints.P3, _, true),
            ("Define core domain model", EffortPoints.P5, H, false),
        });
        AddGoal("roosters", 0, "Product definition + geolocation matching spike", GoalStatus.NotStarted, new[]
        {
            ("Write product one-pager + scope Winter 2027 launch", EffortPoints.P3, M, false),
            ("Spike: geospatial proximity queries", EffortPoints.P3, _, false),
        });

        // Sprint 2 — OVERLOADED (~39 pts): two projects want heavy focus at once.
        AddGoal("buddynetworks", 1, "Cut over to maintenance: monitoring + content-ops runbook", GoalStatus.InProgress, new[]
        {
            ("Wire uptime + error alerting dashboards", EffortPoints.P5, M, false),
            ("Write content-ops runbook", EffortPoints.P3, _, false),
            ("Incident dry-run across sites", EffortPoints.P5, _, false),
        });
        AddGoal("bawdyrooster", 1, "Auth + user profile vertical slice", GoalStatus.AtRisk, new[]
        {
            ("Identity + login/registration", EffortPoints.P5, H, false),
            ("Profile CRUD + avatar upload", EffortPoints.P5, M, false),
            ("Spike: realtime feed approach", EffortPoints.P8, H, false),
        });
        AddGoal("roosters", 1, "Account and profile foundation", GoalStatus.NotStarted, new[]
        {
            ("Account model + auth", EffortPoints.P5, M, false),
            ("Profile + media upload", EffortPoints.P3, _, false),
        });

        // Sprint 3 — healthy again (~21 pts): steady MVP progress.
        AddGoal("buddynetworks", 2, "First maintenance cycle: triage and incremental fixes", GoalStatus.NotStarted, new[]
        {
            ("Triage backlog + prioritize", EffortPoints.P2, _, false),
            ("Ship two incremental fixes", EffortPoints.P3, _, false),
        });
        AddGoal("bawdyrooster", 2, "Post / feed MVP", GoalStatus.NotStarted, new[]
        {
            ("Post composer", EffortPoints.P5, M, false),
            ("Feed render + pagination", EffortPoints.P3, _, false),
        });
        AddGoal("roosters", 2, "Nearby grid MVP", GoalStatus.NotStarted, new[]
        {
            ("Nearby grid UI", EffortPoints.P5, M, false),
            ("Proximity query wired end-to-end", EffortPoints.P3, _, false),
        });

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded goals + tasks for the first 3 sprints across 3 projects.");
    }

    private static async Task SeedSingleUserAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var email = config["SeedUser:Email"];
        var password = config["SeedUser:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "SeedUser:Email / SeedUser:Password not configured — no login user created. " +
                "Set them via user-secrets or environment variables.");
            return;
        }

        var users = services.GetRequiredService<UserManager<IdentityUser>>();
        if (await users.FindByEmailAsync(email) is not null) return;

        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user, password);
        if (result.Succeeded)
            logger.LogInformation("Seeded single login user {Email}.", email);
        else
            logger.LogError("Failed to seed login user: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
