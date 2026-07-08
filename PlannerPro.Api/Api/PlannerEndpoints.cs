using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PlannerPro.Api.Data;
using PlannerPro.Api.Domain;

namespace PlannerPro.Api.Api;

public static class PlannerEndpoints
{
    /// <summary>Sprint is overloaded when total points across all projects exceed this.</summary>
    public const int OverloadThreshold = 24;

    private static readonly int[] ValidPoints = [1, 2, 3, 5, 8, 13, 21];

    public static IEndpointRouteBuilder MapPlannerApi(this IEndpointRouteBuilder app)
    {
        // Single-user tool: everything here requires the auth cookie, and every
        // mutation (non-GET) must carry a valid antiforgery token.
        var api = app.MapGroup("/api")
            .RequireAuthorization()
            .AddEndpointFilter(AntiforgeryFilter);

        // --- Sprints ---
        api.MapGet("/sprints", async (PlannerDbContext db) =>
            await db.Sprints.OrderBy(s => s.Number)
                .Select(s => new SprintDto(s.Id, s.Number, s.StartDate, s.EndDate))
                .ToListAsync());

        api.MapGet("/sprints/current", async (PlannerDbContext db) =>
        {
            var sprint = await ResolveCurrentOrNextAsync(db);
            return sprint is null
                ? Results.NotFound()
                : Results.Ok(new SprintDto(sprint.Id, sprint.Number, sprint.StartDate, sprint.EndDate));
        });

        api.MapGet("/sprints/{id:int}/board", async (int id, PlannerDbContext db) =>
        {
            var sprint = await db.Sprints.FindAsync(id);
            if (sprint is null) return Results.NotFound();

            var projects = await db.Projects.Where(p => p.IsActive)
                .OrderBy(p => p.SortOrder).ToListAsync();

            var goals = await db.SprintGoals
                .Where(g => g.SprintId == id)
                .Include(g => g.Tasks)
                .ToListAsync();

            var users = await db.Users.OrderBy(u => u.DisplayName).ToListAsync();
            var names = users.ToDictionary(u => u.Id, u => u.DisplayName);
            var overrides = await db.SprintCapacities.Where(c => c.SprintId == id)
                .ToDictionaryAsync(c => c.UserId, c => c.Points);

            var columns = new List<BoardColumnDto>();
            var total = 0;
            foreach (var p in projects)
            {
                var goal = goals.FirstOrDefault(g => g.ProjectId == p.Id);
                var tasks = goal?.Tasks.OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
                    .Select(t => ToTaskDto(t, names))
                    .ToList() ?? [];
                var points = tasks.Sum(t => t.Points);
                total += points;

                columns.Add(new BoardColumnDto(
                    p.Id, p.Name, p.Slug, p.ColorHex,
                    goal?.Id, goal?.GoalText, goal?.Notes, goal?.Status, points, tasks));
            }

            // Per-user load across all projects this sprint, vs each user's capacity.
            var allTasks = goals.SelectMany(g => g.Tasks).ToList();
            var loadByUser = allTasks.Where(t => t.AssigneeId is not null)
                .GroupBy(t => t.AssigneeId!)
                .ToDictionary(gr => gr.Key, gr => gr.Sum(t => (int)t.Points));
            var unassigned = allTasks.Where(t => t.AssigneeId is null).Sum(t => (int)t.Points);

            var capacity = users.Select(u =>
            {
                var assigned = loadByUser.GetValueOrDefault(u.Id, 0);
                var cap = overrides.TryGetValue(u.Id, out var o) ? o : u.DefaultCapacityPoints;
                return new UserLoadDto(u.Id, u.DisplayName, assigned, cap, assigned > cap);
            }).ToList();

            // Team ceiling = sum of each member's sprint capacity (per-person, not the
            // legacy global 24). Fall back to the global threshold if there are no users.
            var teamCapacity = capacity.Sum(u => u.Capacity);
            if (teamCapacity <= 0) teamCapacity = OverloadThreshold;

            var prev = await Neighbour(db, sprint.Number, -1);
            var next = await Neighbour(db, sprint.Number, +1);

            return Results.Ok(new BoardDto(
                new SprintDto(sprint.Id, sprint.Number, sprint.StartDate, sprint.EndDate),
                prev, next,
                total, teamCapacity, total > teamCapacity,
                columns, capacity, unassigned));
        });

        // --- Timeline / roadmap: every sprint × every project, with rolled-up effort ---
        api.MapGet("/timeline", async (PlannerDbContext db) =>
        {
            var projects = await db.Projects.Where(p => p.IsActive).OrderBy(p => p.SortOrder)
                .Select(p => new ProjectDto(p.Id, p.Name, p.Slug, p.ColorHex)).ToListAsync();
            var sprints = await db.Sprints.OrderBy(s => s.Number).ToListAsync();
            var goals = await db.SprintGoals.Include(g => g.Tasks).ToListAsync();
            var goalMap = goals.ToDictionary(g => (g.SprintId, g.ProjectId));

            var rows = new List<TimelineRowDto>(sprints.Count);
            foreach (var s in sprints)
            {
                var cells = new List<TimelineCellDto>(projects.Count);
                var total = 0;
                foreach (var p in projects)
                {
                    goalMap.TryGetValue((s.Id, p.Id), out var g);
                    var pts = g?.Tasks.Sum(t => (int)t.Points) ?? 0;
                    total += pts;
                    cells.Add(new TimelineCellDto(p.Id, g?.GoalText, g?.Status, pts));
                }
                rows.Add(new TimelineRowDto(
                    new SprintDto(s.Id, s.Number, s.StartDate, s.EndDate),
                    total, total > OverloadThreshold, cells));
            }

            return Results.Ok(new TimelineDto(OverloadThreshold, projects, rows));
        });

        // --- Goal upsert (lazy create) ---
        api.MapPut("/sprints/{sprintId:int}/projects/{projectId:int}/goal",
            async (int sprintId, int projectId, UpsertGoalRequest req, PlannerDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.GoalText))
                return Results.BadRequest("GoalText is required.");

            var goal = await db.SprintGoals
                .FirstOrDefaultAsync(g => g.SprintId == sprintId && g.ProjectId == projectId);

            var notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes;

            if (goal is null)
            {
                if (!await db.Sprints.AnyAsync(s => s.Id == sprintId) ||
                    !await db.Projects.AnyAsync(p => p.Id == projectId))
                    return Results.NotFound();

                goal = new SprintGoal { SprintId = sprintId, ProjectId = projectId, GoalText = req.GoalText.Trim(), Notes = notes, Status = req.Status };
                db.SprintGoals.Add(goal);
            }
            else
            {
                goal.GoalText = req.GoalText.Trim();
                goal.Notes = notes;
                goal.Status = req.Status;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { goal.Id, goal.GoalText, goal.Notes, goal.Status });
        });

        // --- Tasks (create by sprint+project, lazy-creating the goal if needed) ---
        api.MapPost("/sprints/{sprintId:int}/projects/{projectId:int}/tasks",
            async (int sprintId, int projectId, CreateTaskRequest req, PlannerDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Label))
                return Results.BadRequest("Label is required.");
            if (!ValidPoints.Contains(req.Points))
                return Results.BadRequest($"Points must be one of {string.Join(", ", ValidPoints)}.");

            var goal = await db.SprintGoals
                .Include(g => g.Tasks)
                .FirstOrDefaultAsync(g => g.SprintId == sprintId && g.ProjectId == projectId);

            if (goal is null)
            {
                if (!await db.Sprints.AnyAsync(s => s.Id == sprintId) ||
                    !await db.Projects.AnyAsync(p => p.Id == projectId))
                    return Results.NotFound();

                goal = new SprintGoal { SprintId = sprintId, ProjectId = projectId, GoalText = "" };
                db.SprintGoals.Add(goal);
            }

            var nextOrder = goal.Tasks.Count == 0 ? 0 : goal.Tasks.Max(t => t.SortOrder) + 1;
            var task = new PlannerTask
            {
                Label = req.Label.Trim(),
                Points = (EffortPoints)req.Points,
                Priority = req.Priority,
                SortOrder = nextOrder,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            goal.Tasks.Add(task);
            await db.SaveChangesAsync();

            return Results.Ok(await ToTaskDtoAsync(task, db));
        });

        api.MapPatch("/tasks/{taskId:int}", async (int taskId, UpdateTaskRequest req, PlannerDbContext db) =>
        {
            var task = await db.Tasks.FindAsync(taskId);
            if (task is null) return Results.NotFound();

            if (req.Label is not null)
            {
                if (string.IsNullOrWhiteSpace(req.Label)) return Results.BadRequest("Label cannot be blank.");
                task.Label = req.Label.Trim();
            }
            // null = leave unchanged; empty/whitespace = clear.
            if (req.Description is not null)
                task.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description;
            if (req.Points is int pts)
            {
                if (!ValidPoints.Contains(pts))
                    return Results.BadRequest($"Points must be one of {string.Join(", ", ValidPoints)}.");
                task.Points = (EffortPoints)pts;
            }
            if (req.SortOrder is int so) task.SortOrder = so;
            if (req.IsDone is bool done)
            {
                task.IsDone = done;
                task.CompletedAt = done ? DateTimeOffset.UtcNow : null;
            }

            await db.SaveChangesAsync();
            return Results.Ok(await ToTaskDtoAsync(task, db));
        });

        api.MapPatch("/tasks/{taskId:int}/priority", async (int taskId, SetPriorityRequest req, PlannerDbContext db) =>
        {
            var task = await db.Tasks.FindAsync(taskId);
            if (task is null) return Results.NotFound();
            task.Priority = req.Priority; // null clears it
            await db.SaveChangesAsync();
            return Results.Ok(await ToTaskDtoAsync(task, db));
        });

        api.MapPatch("/tasks/{taskId:int}/assignee", async (int taskId, SetAssigneeRequest req, PlannerDbContext db) =>
        {
            var task = await db.Tasks.FindAsync(taskId);
            if (task is null) return Results.NotFound();

            if (req.AssigneeId is not null && !await db.Users.AnyAsync(u => u.Id == req.AssigneeId))
                return Results.BadRequest("Unknown assignee.");

            task.AssigneeId = req.AssigneeId; // null unassigns
            await db.SaveChangesAsync();
            return Results.Ok(await ToTaskDtoAsync(task, db));
        });

        api.MapDelete("/tasks/{taskId:int}", async (int taskId, PlannerDbContext db) =>
        {
            var task = await db.Tasks.FindAsync(taskId);
            if (task is null) return Results.NotFound();
            db.Tasks.Remove(task);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

    /// <summary>Maps a task to its DTO using a preloaded id→display-name lookup.</summary>
    private static TaskDto ToTaskDto(PlannerTask t, IReadOnlyDictionary<string, string> names) =>
        new(t.Id, t.Label, t.Description, t.IsDone, (int)t.Points, t.Priority, t.SortOrder,
            t.AssigneeId,
            t.AssigneeId is not null && names.TryGetValue(t.AssigneeId, out var n) ? n : null);

    /// <summary>Maps a single task to its DTO, resolving the assignee name from the DB.</summary>
    private static async Task<TaskDto> ToTaskDtoAsync(PlannerTask t, PlannerDbContext db)
    {
        var name = t.AssigneeId is null
            ? null
            : await db.Users.Where(u => u.Id == t.AssigneeId)
                .Select(u => u.DisplayName).FirstOrDefaultAsync();
        return new TaskDto(t.Id, t.Label, t.Description, t.IsDone, (int)t.Points, t.Priority, t.SortOrder, t.AssigneeId, name);
    }

    /// <summary>Validates the antiforgery token on state-changing requests; GETs pass through.</summary>
    internal static async ValueTask<object?> AntiforgeryFilter(
        EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var http = ctx.HttpContext;
        var method = http.Request.Method;
        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method))
        {
            var antiforgery = http.RequestServices.GetRequiredService<IAntiforgery>();
            try
            {
                await antiforgery.ValidateRequestAsync(http);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem("Invalid or missing antiforgery token.", statusCode: StatusCodes.Status400BadRequest);
            }
        }
        return await next(ctx);
    }

    /// <summary>Sprint containing today; else the next upcoming sprint; else the last.</summary>
    private static async Task<Sprint?> ResolveCurrentOrNextAsync(PlannerDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var containing = await db.Sprints
            .Where(s => s.StartDate <= today && today <= s.EndDate)
            .OrderBy(s => s.Number).FirstOrDefaultAsync();
        if (containing is not null) return containing;

        var upcoming = await db.Sprints
            .Where(s => s.StartDate > today)
            .OrderBy(s => s.Number).FirstOrDefaultAsync();
        if (upcoming is not null) return upcoming;

        return await db.Sprints.OrderByDescending(s => s.Number).FirstOrDefaultAsync();
    }

    private static async Task<SprintDto?> Neighbour(PlannerDbContext db, int number, int delta)
    {
        var s = await db.Sprints.FirstOrDefaultAsync(x => x.Number == number + delta);
        return s is null ? null : new SprintDto(s.Id, s.Number, s.StartDate, s.EndDate);
    }
}
