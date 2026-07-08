namespace PlannerPro.Api.Domain;

/// <summary>A two-week sprint window. Auto-generated on a rolling biweekly
/// cadence starting Mon 2026-07-13, extending through end of 2027 (and beyond
/// as needed). Sprints are pre-generated so the timeline has a full grid;
/// goals within them are created lazily.</summary>
public class Sprint
{
    public int Id { get; set; }

    /// <summary>Sequential sprint number (1, 2, 3, ...).</summary>
    public int Number { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public ICollection<SprintGoal> Goals { get; set; } = new List<SprintGoal>();
}
