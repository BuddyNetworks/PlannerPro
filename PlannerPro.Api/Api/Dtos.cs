using PlannerPro.Api.Domain;

namespace PlannerPro.Api.Api;

public record SprintDto(int Id, int Number, DateOnly StartDate, DateOnly EndDate);

public record TaskDto(
    int Id,
    string Label,
    bool IsDone,
    int Points,
    TaskPriority? Priority,
    int SortOrder);

/// <summary>One project's column on the sprint board. GoalId/GoalText/Status are
/// null when no goal has been set yet for this (sprint, project) pairing.</summary>
public record BoardColumnDto(
    int ProjectId,
    string ProjectName,
    string Slug,
    string ColorHex,
    int? GoalId,
    string? GoalText,
    GoalStatus? Status,
    int Points,
    List<TaskDto> Tasks);

/// <summary>The full sprint board: the sprint, its neighbours for navigation,
/// the three project columns, and the rolled-up effort/overload signal.</summary>
public record BoardDto(
    SprintDto Sprint,
    SprintDto? Prev,
    SprintDto? Next,
    int TotalPoints,
    int OverloadThreshold,
    bool IsOverloaded,
    List<BoardColumnDto> Columns);

// --- timeline / roadmap ---
public record ProjectDto(int Id, string Name, string Slug, string ColorHex);

public record TimelineCellDto(int ProjectId, string? GoalText, GoalStatus? Status, int Points);

public record TimelineRowDto(SprintDto Sprint, int TotalPoints, bool IsOverloaded, List<TimelineCellDto> Cells);

public record TimelineDto(int OverloadThreshold, List<ProjectDto> Projects, List<TimelineRowDto> Rows);

// --- write payloads ---
public record UpsertGoalRequest(string GoalText, GoalStatus Status);
public record CreateTaskRequest(string Label, int Points, TaskPriority? Priority);
public record UpdateTaskRequest(string? Label, bool? IsDone, int? Points, int? SortOrder);
// Dedicated so null can mean "clear priority" (unambiguous vs. a partial update).
public record SetPriorityRequest(TaskPriority? Priority);
