namespace PlannerPro.Api.Domain;

/// <summary>A per-user, per-sprint capacity override in story points. When a row
/// exists for (User, Sprint) it replaces the user's <see cref="ApplicationUser.DefaultCapacityPoints"/>
/// for that sprint only — e.g. reduced during vacation, higher during a crunch.
/// Absence of a row means "use the default". Unique on (UserId, SprintId).</summary>
public class SprintCapacity
{
    public int Id { get; set; }

    public required string UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public int SprintId { get; set; }
    public Sprint Sprint { get; set; } = null!;

    /// <summary>Capacity in story points for this user in this sprint.</summary>
    public int Points { get; set; }
}
