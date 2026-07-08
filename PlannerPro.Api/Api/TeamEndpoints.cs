using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlannerPro.Api.Data;
using PlannerPro.Api.Domain;

namespace PlannerPro.Api.Api;

/// <summary>Team &amp; capacity-planning API: user management (admin-only) and the
/// per-user, per-sprint capacity matrix. Writes are guarded by <see cref="RequireAdmin"/>;
/// reading the capacity matrix is allowed for any authenticated user.</summary>
public static class TeamEndpoints
{
    private const int MaxCapacity = 200;

    public static IEndpointRouteBuilder MapTeamApi(this IEndpointRouteBuilder app)
    {
        // Authenticated + antiforgery, matching the rest of the planner API.
        var api = app.MapGroup("/api")
            .RequireAuthorization()
            .AddEndpointFilter(PlannerEndpoints.AntiforgeryFilter);

        // ---------------- Users (admin only) ----------------
        api.MapGet("/users", async (PlannerDbContext db) =>
            await db.Users.OrderBy(u => u.DisplayName)
                .Select(u => new UserDto(u.Id, u.Email!, u.DisplayName, u.IsAdmin, u.DefaultCapacityPoints))
                .ToListAsync())
            .AddEndpointFilter(RequireAdmin);

        api.MapPost("/users", async (CreateUserRequest req, UserManager<ApplicationUser> mgr) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email)) return Results.BadRequest("Email is required.");
            if (string.IsNullOrWhiteSpace(req.DisplayName)) return Results.BadRequest("Display name is required.");
            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
                return Results.BadRequest("Password must be at least 8 characters.");
            if (req.DefaultCapacityPoints is < 0 or > MaxCapacity)
                return Results.BadRequest($"Default capacity must be between 0 and {MaxCapacity}.");

            var user = new ApplicationUser
            {
                UserName = req.Email.Trim(),
                Email = req.Email.Trim(),
                EmailConfirmed = true,
                DisplayName = req.DisplayName.Trim(),
                IsAdmin = req.IsAdmin,
                DefaultCapacityPoints = req.DefaultCapacityPoints,
            };
            var result = await mgr.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                return Results.BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

            return Results.Ok(new UserDto(user.Id, user.Email!, user.DisplayName, user.IsAdmin, user.DefaultCapacityPoints));
        }).AddEndpointFilter(RequireAdmin);

        api.MapPatch("/users/{id}", async (string id, UpdateUserRequest req, PlannerDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();

            if (req.DisplayName is not null)
            {
                if (string.IsNullOrWhiteSpace(req.DisplayName)) return Results.BadRequest("Display name cannot be blank.");
                user.DisplayName = req.DisplayName.Trim();
            }
            if (req.DefaultCapacityPoints is int cap)
            {
                if (cap is < 0 or > MaxCapacity) return Results.BadRequest($"Default capacity must be between 0 and {MaxCapacity}.");
                user.DefaultCapacityPoints = cap;
            }
            if (req.IsAdmin is bool admin)
            {
                // Don't allow demoting the last remaining admin.
                if (!admin && user.IsAdmin && await db.Users.CountAsync(u => u.IsAdmin) <= 1)
                    return Results.BadRequest("Cannot remove the last admin.");
                user.IsAdmin = admin;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new UserDto(user.Id, user.Email!, user.DisplayName, user.IsAdmin, user.DefaultCapacityPoints));
        }).AddEndpointFilter(RequireAdmin);

        api.MapPost("/users/{id}/password", async (string id, ResetPasswordRequest req, UserManager<ApplicationUser> mgr) =>
        {
            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
                return Results.BadRequest("Password must be at least 8 characters.");
            var user = await mgr.FindByIdAsync(id);
            if (user is null) return Results.NotFound();

            var token = await mgr.GeneratePasswordResetTokenAsync(user);
            var result = await mgr.ResetPasswordAsync(user, token, req.Password);
            return result.Succeeded
                ? Results.NoContent()
                : Results.BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));
        }).AddEndpointFilter(RequireAdmin);

        api.MapDelete("/users/{id}", async (string id, HttpContext ctx, UserManager<ApplicationUser> mgr, PlannerDbContext db) =>
        {
            var user = await mgr.FindByIdAsync(id);
            if (user is null) return Results.NotFound();

            if (id == mgr.GetUserId(ctx.User)) return Results.BadRequest("You cannot delete your own account.");
            if (user.IsAdmin && await db.Users.CountAsync(u => u.IsAdmin) <= 1)
                return Results.BadRequest("Cannot delete the last admin.");

            var result = await mgr.DeleteAsync(user);
            return result.Succeeded
                ? Results.NoContent()
                : Results.BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));
        }).AddEndpointFilter(RequireAdmin);

        // ---------------- Capacity matrix ----------------
        // Read: any authenticated user. Window defaults to the current sprint forward.
        api.MapGet("/capacity", async (PlannerDbContext db, int? fromSprint, int? count) =>
        {
            var take = Math.Clamp(count ?? 6, 1, 26);
            var startNumber = fromSprint ?? await ResolveCurrentSprintNumberAsync(db);

            var sprints = await db.Sprints.Where(s => s.Number >= startNumber)
                .OrderBy(s => s.Number).Take(take).ToListAsync();
            var sprintIds = sprints.Select(s => s.Id).ToList();

            var users = await db.Users.OrderBy(u => u.DisplayName).ToListAsync();

            var overrideMap = (await db.SprintCapacities
                    .Where(c => sprintIds.Contains(c.SprintId)).ToListAsync())
                .ToDictionary(c => (c.UserId, c.SprintId), c => c.Points);

            var loadMap = (await db.Tasks
                    .Where(t => t.AssigneeId != null && sprintIds.Contains(t.SprintGoal.SprintId))
                    .GroupBy(t => new { t.AssigneeId, t.SprintGoal.SprintId })
                    .Select(g => new { g.Key.AssigneeId, g.Key.SprintId, Points = g.Sum(t => (int)t.Points) })
                    .ToListAsync())
                .ToDictionary(x => (x.AssigneeId!, x.SprintId), x => x.Points);

            var rows = users.Select(u =>
            {
                var cells = sprints.Select(s =>
                {
                    var assigned = loadMap.GetValueOrDefault((u.Id, s.Id), 0);
                    var isOverride = overrideMap.TryGetValue((u.Id, s.Id), out var ov);
                    var cap = isOverride ? ov : u.DefaultCapacityPoints;
                    return new CapacityCellDto(s.Id, assigned, cap, isOverride, assigned > cap);
                }).ToList();
                return new CapacityRowDto(
                    new UserDto(u.Id, u.Email!, u.DisplayName, u.IsAdmin, u.DefaultCapacityPoints), cells);
            }).ToList();

            var sprintDtos = sprints.Select(s => new SprintDto(s.Id, s.Number, s.StartDate, s.EndDate)).ToList();
            return Results.Ok(new CapacityMatrixDto(sprintDtos, rows));
        });

        // Write a per-sprint capacity override (admin only). Null points clears it.
        api.MapPut("/sprints/{sprintId:int}/users/{userId}/capacity",
            async (int sprintId, string userId, SetCapacityRequest req, PlannerDbContext db) =>
        {
            if (!await db.Sprints.AnyAsync(s => s.Id == sprintId)) return Results.NotFound();
            if (!await db.Users.AnyAsync(u => u.Id == userId)) return Results.NotFound();
            if (req.Points is int p && (p < 0 || p > MaxCapacity))
                return Results.BadRequest($"Capacity must be between 0 and {MaxCapacity}.");

            var existing = await db.SprintCapacities
                .FirstOrDefaultAsync(c => c.SprintId == sprintId && c.UserId == userId);

            if (req.Points is null)
            {
                if (existing is not null)
                {
                    db.SprintCapacities.Remove(existing);
                    await db.SaveChangesAsync();
                }
                return Results.NoContent();
            }

            if (existing is null)
                db.SprintCapacities.Add(new SprintCapacity { SprintId = sprintId, UserId = userId, Points = req.Points.Value });
            else
                existing.Points = req.Points.Value;

            await db.SaveChangesAsync();
            return Results.Ok(new { sprintId, userId, points = req.Points.Value });
        }).AddEndpointFilter(RequireAdmin);

        return app;
    }

    /// <summary>Endpoint filter: 403 unless the current user's DB record is an admin.
    /// A live lookup (not a cached claim) so toggling admin takes effect immediately.</summary>
    private static async ValueTask<object?> RequireAdmin(
        EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var mgr = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await mgr.GetUserAsync(ctx.HttpContext.User);
        if (user is null || !user.IsAdmin)
            return Results.Problem("Admin access required.", statusCode: StatusCodes.Status403Forbidden);
        return await next(ctx);
    }

    /// <summary>Sprint number containing today; else the next upcoming; else the first.</summary>
    private static async Task<int> ResolveCurrentSprintNumberAsync(PlannerDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var containing = await db.Sprints
            .Where(s => s.StartDate <= today && today <= s.EndDate)
            .OrderBy(s => s.Number).FirstOrDefaultAsync();
        if (containing is not null) return containing.Number;

        var upcoming = await db.Sprints
            .Where(s => s.StartDate > today)
            .OrderBy(s => s.Number).FirstOrDefaultAsync();
        if (upcoming is not null) return upcoming.Number;

        var last = await db.Sprints.OrderByDescending(s => s.Number).FirstOrDefaultAsync();
        return last?.Number ?? 1;
    }
}
