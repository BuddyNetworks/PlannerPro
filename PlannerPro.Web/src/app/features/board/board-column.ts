import { Component, computed, inject, input, signal } from '@angular/core';
import { BoardStore } from '../../core/board-store';
import { MarkdownPipe } from '../../core/markdown.pipe';
import {
  BoardColumn,
  FIBONACCI_POINTS,
  GOAL_STATUSES,
  GoalStatus,
  PRIORITY_ABBR,
  STATUS_LABEL,
  TASK_POINT_WARN,
  TASK_PRIORITIES,
  TaskItem,
  TaskPriority,
} from '../../core/models';

@Component({
  selector: 'app-board-column',
  imports: [MarkdownPipe],
  templateUrl: './board-column.html',
  styleUrl: './board-column.scss',
})
export class BoardColumnComponent {
  readonly column = input.required<BoardColumn>();
  private readonly store = inject(BoardStore);

  openDrawer() {
    this.store.openColumnDrawer(this.column().projectId);
  }

  openGoal() {
    this.store.openGoalDrawer(this.column().projectId);
  }

  openTask(task: TaskItem) {
    this.store.openTaskDrawer(this.column().projectId, task.id);
  }

  /** Assignee options come from the board's per-user capacity list (all users). */
  readonly assignees = computed(() => this.store.value()?.capacity ?? []);

  readonly statuses = GOAL_STATUSES;
  readonly statusLabel = STATUS_LABEL;
  readonly pointOptions = FIBONACCI_POINTS;
  readonly warnAbove = TASK_POINT_WARN;
  readonly priorities = TASK_PRIORITIES;
  readonly priorityAbbr = PRIORITY_ABBR;

  readonly hasGoal = computed(() => !!this.column().goalText);
  readonly completed = computed(() => this.column().tasks.filter((t) => t.isDone).length);

  // --- goal editing ---
  readonly editingGoal = signal(false);
  readonly goalDraft = signal('');

  startEditGoal() {
    this.goalDraft.set(this.column().goalText ?? '');
    this.editingGoal.set(true);
  }

  async saveGoal() {
    const text = this.goalDraft().trim();
    this.editingGoal.set(false);
    if (!text) return;
    await this.store.setGoal(this.column(), text, this.column().status ?? 'NotStarted');
  }

  cancelGoal() {
    this.editingGoal.set(false);
  }

  async onStatusChange(value: string) {
    await this.store.setStatus(this.column(), value as GoalStatus);
  }

  // --- add task ---
  readonly newLabel = signal('');
  readonly newPoints = signal<number>(3);

  async addTask() {
    const label = this.newLabel().trim();
    if (!label) return;
    await this.store.addTask(this.column(), label, this.newPoints(), null);
    this.newLabel.set('');
  }

  // --- task interactions ---
  readonly editingTaskId = signal<number | null>(null);
  readonly taskDraft = signal('');

  toggle(task: TaskItem) {
    this.store.toggleTask(this.column().projectId, task);
  }

  startEditTask(task: TaskItem) {
    this.taskDraft.set(task.label);
    this.editingTaskId.set(task.id);
  }

  async saveTask(task: TaskItem) {
    const label = this.taskDraft().trim();
    this.editingTaskId.set(null);
    if (!label || label === task.label) return;
    await this.store.updateTask(this.column().projectId, task, { label });
  }

  changePoints(task: TaskItem, points: number) {
    this.store.updateTask(this.column().projectId, task, { points: +points });
  }

  changePriority(task: TaskItem, value: string) {
    const priority = value ? (value as TaskPriority) : null;
    this.store.setPriority(this.column().projectId, task, priority);
  }

  changeAssignee(task: TaskItem, value: string) {
    this.store.setAssignee(this.column().projectId, task, value || null);
  }

  initials(name: string | null): string {
    if (!name) return '?';
    const parts = name.trim().split(/\s+/);
    return (parts[0]?.[0] ?? '').concat(parts.length > 1 ? (parts[parts.length - 1][0] ?? '') : '').toUpperCase();
  }

  remove(task: TaskItem) {
    this.store.deleteTask(this.column().projectId, task);
  }
}
