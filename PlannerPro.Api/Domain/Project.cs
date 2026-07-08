namespace PlannerPro.Api.Domain;

/// <summary>One of the (few, fixed) software projects being tracked.
/// Seeded with BuddyNetworks, BawdyRooster, Roosters.</summary>
public class Project
{
    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>URL/CSS-friendly identifier, e.g. "buddynetworks".</summary>
    public required string Slug { get; set; }

    /// <summary>Accent color for the UI, e.g. "#c2410c".</summary>
    public required string ColorHex { get; set; }

    /// <summary>Display order on the board/timeline.</summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<SprintGoal> Goals { get; set; } = new List<SprintGoal>();
}
