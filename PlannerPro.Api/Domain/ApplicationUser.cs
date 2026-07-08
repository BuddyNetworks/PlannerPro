using Microsoft.AspNetCore.Identity;

namespace PlannerPro.Api.Domain;

/// <summary>The login user. Extends IdentityUser with the fields PlannerPro needs:
/// a friendly display name, an admin flag (gates user/capacity management), and a
/// default per-sprint capacity in story points (used when no per-sprint override
/// exists). Multiple users are supported; only admins can manage the team.</summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>Friendly name shown in assignee pickers and the capacity grid.</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>Admins can add/remove users and edit capacity.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Normal story-point capacity per two-week sprint. Overridable per
    /// sprint via <see cref="SprintCapacity"/> (vacation, ramp-up, etc.).</summary>
    public int DefaultCapacityPoints { get; set; } = 24;

    /// <summary>Tasks currently assigned to this user.</summary>
    public ICollection<PlannerTask> AssignedTasks { get; set; } = new List<PlannerTask>();
}
