namespace PlannerPro.Api.Domain;

/// <summary>A task under a SprintGoal — the breakdown of how the goal gets done.
/// Named PlannerTask (not "Task") to avoid clashing with
/// System.Threading.Tasks.Task. Table name is "Tasks".</summary>
public class PlannerTask
{
    public int Id { get; set; }

    public int SprintGoalId { get; set; }
    public SprintGoal SprintGoal { get; set; } = null!;

    public required string Label { get; set; }

    /// <summary>Optional longer task detail, stored as Markdown.</summary>
    public string? Description { get; set; }

    public bool IsDone { get; set; }

    /// <summary>Fibonacci effort estimate; UI warns when above 8.</summary>
    public EffortPoints Points { get; set; } = EffortPoints.P3;

    /// <summary>Optional lightweight priority.</summary>
    public TaskPriority? Priority { get; set; }

    /// <summary>Optional assignee. Null = unassigned. When the assignee is
    /// deleted this is set back to null (the task survives).</summary>
    public string? AssigneeId { get; set; }
    public ApplicationUser? Assignee { get; set; }

    /// <summary>Ordering within its goal.</summary>
    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
