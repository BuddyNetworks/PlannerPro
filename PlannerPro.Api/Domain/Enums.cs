namespace PlannerPro.Api.Domain;

/// <summary>Status of a single project's goal within a sprint.</summary>
public enum GoalStatus
{
    NotStarted = 0,
    InProgress = 1,
    AtRisk = 2,
    Done = 3,
    Deferred = 4,
}

/// <summary>Fibonacci story points. Stored as the underlying point value so
/// effort sums are just SUM(Points). Values above 8 are flagged by the UI as
/// "too big, break it down" (advisory, not blocked).</summary>
public enum EffortPoints
{
    P1 = 1,
    P2 = 2,
    P3 = 3,
    P5 = 5,
    P8 = 8,
    P13 = 13,
    P21 = 21,
}

/// <summary>Optional, lightweight task priority.</summary>
public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
}
