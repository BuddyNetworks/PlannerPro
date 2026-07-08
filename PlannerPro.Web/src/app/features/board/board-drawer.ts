import { Component, computed, effect, inject, signal } from '@angular/core';
import { BoardStore } from '../../core/board-store';
import { MarkdownPipe } from '../../core/markdown.pipe';
import {
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

/**
 * Slide-in editor for the board. It opens in one of three scopes:
 *  - 'column' — goal headline, status, notes, and the whole task list;
 *  - 'goal'   — just the goal headline, status, and Markdown notes;
 *  - 'task'   — a single task's full editor (label, Markdown description, meta).
 * All reads/writes go through the BoardStore.
 */
@Component({
  selector: 'app-board-drawer',
  imports: [MarkdownPipe],
  templateUrl: './board-drawer.html',
  styleUrl: './board-drawer.scss',
})
export class BoardDrawerComponent {
  private readonly store = inject(BoardStore);

  readonly col = this.store.openColumn;
  readonly scope = this.store.drawerScope;
  readonly task = this.store.openTask;
  readonly assignees = computed(() => this.store.value()?.capacity ?? []);

  readonly statuses = GOAL_STATUSES;
  readonly statusLabel = STATUS_LABEL;
  readonly pointOptions = FIBONACCI_POINTS;
  readonly warnAbove = TASK_POINT_WARN;
  readonly priorities = TASK_PRIORITIES;
  readonly priorityAbbr = PRIORITY_ABBR;

  // --- goal drafts ---
  readonly headline = signal('');
  readonly notes = signal('');
  readonly status = signal<GoalStatus>('NotStarted');
  readonly notesPreview = signal(false);
  readonly saving = signal(false);

  // --- single-task drafts (task scope) ---
  readonly tLabel = signal('');
  readonly tDesc = signal('');
  readonly tDescPreview = signal(false);
  readonly tSaving = signal(false);

  private lastKey: string | null = null;

  constructor() {
    // Reset drafts only when the drawer *target* changes (column/scope/task),
    // not on every board refresh — so in-progress edits survive optimistic reloads.
    effect(() => {
      const d = this.store.drawer();
      const c = this.col();
      const t = this.task();
      const key = d ? `${d.projectId}:${d.scope}:${d.taskId ?? ''}` : null;
      if (key === this.lastKey) return;
      this.lastKey = key;

      this.headline.set(c?.goalText ?? '');
      this.notes.set(c?.notes ?? '');
      this.status.set(c?.status ?? 'NotStarted');
      this.notesPreview.set(false);

      this.tLabel.set(t?.label ?? '');
      this.tDesc.set(t?.description ?? '');
      this.tDescPreview.set(false);

      this.descId.set(null);
      this.editingLabelId.set(null);
    });
  }

  close() {
    this.store.closeDrawer();
  }

  openFullColumn() {
    const c = this.col();
    if (c) this.store.openColumnDrawer(c.projectId);
  }

  // --- goal scope ---
  async saveGoal() {
    const c = this.col();
    if (!c) return;
    const text = this.headline().trim();
    if (!text) return;
    this.saving.set(true);
    try {
      await this.store.setGoal(c, text, this.status(), this.notes().trim() || null);
      if (this.scope() === 'goal') this.close();
    } finally {
      this.saving.set(false);
    }
  }

  // --- task scope ---
  async saveTask() {
    const c = this.col();
    const t = this.task();
    if (!c || !t) return;
    const label = this.tLabel().trim();
    if (!label) return;
    this.tSaving.set(true);
    try {
      await this.store.updateTask(c.projectId, t, { label, description: this.tDesc() });
      this.close();
    } finally {
      this.tSaving.set(false);
    }
  }

  async removeTaskAndClose() {
    const c = this.col();
    const t = this.task();
    if (!c || !t) return;
    await this.store.deleteTask(c.projectId, t);
    this.close();
  }

  // --- column-scope task label edit ---
  readonly editingLabelId = signal<number | null>(null);
  readonly labelDraft = signal('');

  startLabel(task: TaskItem) {
    this.labelDraft.set(task.label);
    this.editingLabelId.set(task.id);
  }

  async saveLabel(task: TaskItem) {
    const label = this.labelDraft().trim();
    this.editingLabelId.set(null);
    if (!label || label === task.label) return;
    await this.store.updateTask(this.col()!.projectId, task, { label });
  }

  // --- column-scope task description edit (one open at a time) ---
  readonly descId = signal<number | null>(null);
  readonly descDraft = signal('');
  readonly descPreview = signal(false);

  toggleDesc(task: TaskItem) {
    if (this.descId() === task.id) {
      this.descId.set(null);
      return;
    }
    this.descDraft.set(task.description ?? '');
    this.descPreview.set(false);
    this.descId.set(task.id);
  }

  async saveDesc(task: TaskItem) {
    await this.store.updateTask(this.col()!.projectId, task, { description: this.descDraft() });
    this.descId.set(null);
  }

  openTaskScope(task: TaskItem) {
    this.store.openTaskDrawer(this.col()!.projectId, task.id);
  }

  // --- immediate task actions (shared) ---
  toggle(task: TaskItem) {
    this.store.toggleTask(this.col()!.projectId, task);
  }
  changePoints(task: TaskItem, points: number) {
    this.store.updateTask(this.col()!.projectId, task, { points: +points });
  }
  changePriority(task: TaskItem, value: string) {
    this.store.setPriority(this.col()!.projectId, task, value ? (value as TaskPriority) : null);
  }
  changeAssignee(task: TaskItem, value: string) {
    this.store.setAssignee(this.col()!.projectId, task, value || null);
  }
  remove(task: TaskItem) {
    this.store.deleteTask(this.col()!.projectId, task);
  }

  // --- add task (column scope) ---
  readonly newLabel = signal('');
  readonly newPoints = signal<number>(3);

  async addTask() {
    const c = this.col();
    if (!c) return;
    const label = this.newLabel().trim();
    if (!label) return;
    await this.store.addTask(c, label, this.newPoints(), null);
    this.newLabel.set('');
  }
}
