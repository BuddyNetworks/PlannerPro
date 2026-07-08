using PlannerPro.Api.Domain;

namespace PlannerPro.Api.Api;

public record SprintDto(int Id, int Number, DateOnly StartDate, DateOnly EndDate);

public record TaskDto(
    int Id,
    string Label,
    string? Description,
    bool IsDone,
    int Points,
    TaskPriority? Priority,
    int SortOrder,
    string? AssigneeId,
    string? AssigneeName);

/// <summary>One project's column on the sprint board. GoalId/GoalText/Status are
/// null when no goal has been set yet for this (sprint, project) pairing.</summary>
public record BoardColumnDto(
    int ProjectId,
    string ProjectName,
    string Slug,
    string ColorHex,
    int? GoalId,
    string? GoalText,
    string? Notes,
    GoalStatus? Status,
    int Points,
    List<TaskDto> Tasks);

/// <summary>Per-user load vs capacity for a single sprint. AssignedPoints is the
/// sum of that user's assigned task points; Capacity is their per-sprint override
/// or default; IsOver flags load beyond capacity.</summary>
public record UserLoadDto(
    string UserId,
    string DisplayName,
    int AssignedPoints,
    int Capacity,
    bool IsOver);

/// <summary>The full sprint board: the sprint, its neighbours for navigation,
/// the project columns, the rolled-up effort/overload signal, and the per-user
/// capacity breakdown for this sprint.</summary>
public record BoardDto(
    SprintDto Sprint,
    SprintDto? Prev,
    SprintDto? Next,
    int TotalPoints,
    int TeamCapacity,
    bool IsOverloaded,
    List<BoardColumnDto> Columns,
    List<UserLoadDto> Capacity,
    int UnassignedPoints);

// --- timeline / roadmap ---
public record ProjectDto(int Id, string Name, string Slug, string ColorHex);

public record TimelineCellDto(int ProjectId, string? GoalText, GoalStatus? Status, int Points);

public record TimelineRowDto(SprintDto Sprint, int TotalPoints, bool IsOverloaded, List<TimelineCellDto> Cells);

public record TimelineDto(int OverloadThreshold, List<ProjectDto> Projects, List<TimelineRowDto> Rows);

// --- write payloads ---
public record UpsertGoalRequest(string GoalText, string? Notes, GoalStatus Status);
public record CreateTaskRequest(string Label, int Points, TaskPriority? Priority);
// Description: null = leave unchanged, "" = clear it.
public record UpdateTaskRequest(string? Label, string? Description, bool? IsDone, int? Points, int? SortOrder);
// Dedicated so null can mean "clear priority" (unambiguous vs. a partial update).
public record SetPriorityRequest(TaskPriority? Priority);
// Dedicated so null can mean "unassign".
public record SetAssigneeRequest(string? AssigneeId);

// --- team / users ---
public record UserDto(
    string Id,
    string Email,
    string DisplayName,
    bool IsAdmin,
    int DefaultCapacityPoints);

public record CreateUserRequest(
    string Email,
    string Password,
    string DisplayName,
    bool IsAdmin,
    int DefaultCapacityPoints);

public record UpdateUserRequest(string? DisplayName, bool? IsAdmin, int? DefaultCapacityPoints);
public record ResetPasswordRequest(string Password);

// --- capacity planning matrix ---
public record CapacityCellDto(
    int SprintId,
    int AssignedPoints,
    int Capacity,
    bool IsOverride,
    bool IsOver);

public record CapacityRowDto(UserDto User, List<CapacityCellDto> Cells);

public record CapacityMatrixDto(List<SprintDto> Sprints, List<CapacityRowDto> Rows);

// null Points clears the per-sprint override (falls back to the user default).
public record SetCapacityRequest(int? Points);
