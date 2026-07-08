export type GoalStatus = 'NotStarted' | 'InProgress' | 'AtRisk' | 'Done' | 'Deferred';
export type TaskPriority = 'Low' | 'Medium' | 'High';

export const FIBONACCI_POINTS = [1, 2, 3, 5, 8, 13, 21] as const;
export type Points = (typeof FIBONACCI_POINTS)[number];

/** UI flags a task as too big above this. */
export const TASK_POINT_WARN = 8;

export const GOAL_STATUSES: GoalStatus[] = ['NotStarted', 'InProgress', 'AtRisk', 'Done', 'Deferred'];

export const TASK_PRIORITIES: TaskPriority[] = ['Low', 'Medium', 'High'];

/** Single-letter badge for compact task rows. */
export const PRIORITY_ABBR: Record<TaskPriority, string> = {
  Low: 'L',
  Medium: 'M',
  High: 'H',
};

export const STATUS_LABEL: Record<GoalStatus, string> = {
  NotStarted: 'Not started',
  InProgress: 'In progress',
  AtRisk: 'At risk',
  Done: 'Done',
  Deferred: 'Deferred',
};

export interface Sprint {
  id: number;
  number: number;
  startDate: string; // ISO date (yyyy-MM-dd)
  endDate: string;
}

export interface TaskItem {
  id: number;
  label: string;
  description: string | null; // markdown
  isDone: boolean;
  points: number;
  priority: TaskPriority | null;
  sortOrder: number;
  assigneeId: string | null;
  assigneeName: string | null;
}

export interface BoardColumn {
  projectId: number;
  projectName: string;
  slug: string;
  colorHex: string;
  goalId: number | null;
  goalText: string | null;
  notes: string | null; // markdown
  status: GoalStatus | null;
  points: number;
  tasks: TaskItem[];
}

export interface Project {
  id: number;
  name: string;
  slug: string;
  colorHex: string;
}

export interface TimelineCell {
  projectId: number;
  goalText: string | null;
  status: GoalStatus | null;
  points: number;
}

export interface TimelineRow {
  sprint: Sprint;
  totalPoints: number;
  isOverloaded: boolean;
  cells: TimelineCell[];
}

export interface Timeline {
  overloadThreshold: number;
  projects: Project[];
  rows: TimelineRow[];
}

/** Per-user load vs capacity for a sprint (board summary strip). */
export interface UserLoad {
  userId: string;
  displayName: string;
  assignedPoints: number;
  capacity: number;
  isOver: boolean;
}

export interface Board {
  sprint: Sprint;
  prev: Sprint | null;
  next: Sprint | null;
  totalPoints: number;
  teamCapacity: number;
  isOverloaded: boolean;
  columns: BoardColumn[];
  capacity: UserLoad[];
  unassignedPoints: number;
}

// --- team / users ---
export interface User {
  id: string;
  email: string;
  displayName: string;
  isAdmin: boolean;
  defaultCapacityPoints: number;
}

export interface CreateUserPayload {
  email: string;
  password: string;
  displayName: string;
  isAdmin: boolean;
  defaultCapacityPoints: number;
}

export type UpdateUserPayload = Partial<Pick<User, 'displayName' | 'isAdmin' | 'defaultCapacityPoints'>>;

// --- capacity planning matrix ---
export interface CapacityCell {
  sprintId: number;
  assignedPoints: number;
  capacity: number;
  isOverride: boolean;
  isOver: boolean;
}

export interface CapacityRow {
  user: User;
  cells: CapacityCell[];
}

export interface CapacityMatrix {
  sprints: Sprint[];
  rows: CapacityRow[];
}
