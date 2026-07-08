namespace PlannerPro.Api.Domain;

/// <summary>The core join entity: exactly one goal per Project × Sprint.
/// Holds the single-outcome goal statement and its status. Effort is NOT
/// stored here — it is the sum of the child tasks' points.
/// A unique index on (SprintId, ProjectId) enforces the "one goal" rule.
/// Rows are created lazily — only once a goal is set for that pairing.</summary>
public class SprintGoal
{
    public int Id { get; set; }

    public int SprintId { get; set; }
    public Sprint Sprint { get; set; } = null!;

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>Short, single-outcome statement of the goal (plain-text headline).</summary>
    public required string GoalText { get; set; }

    /// <summary>Optional longer notes/context for the goal, stored as Markdown.</summary>
    public string? Notes { get; set; }

    public GoalStatus Status { get; set; } = GoalStatus.NotStarted;

    public ICollection<PlannerTask> Tasks { get; set; } = new List<PlannerTask>();
}
