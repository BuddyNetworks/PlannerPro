import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Board, BoardColumn, GoalStatus, Sprint, TaskItem, TaskPriority } from './models';

/**
 * Signal-based store for the Sprint Board. Reads use httpResource (reactive to
 * the selected sprint); writes update the local board signal optimistically for
 * instant feedback, then reload() reconciles with the server's recomputed effort.
 */
@Injectable({ providedIn: 'root' })
export class BoardStore {
  private readonly http = inject(HttpClient);

  readonly sprintId = signal<number | null>(null);

  readonly board = httpResource<Board>(() => {
    const id = this.sprintId();
    return id == null ? undefined : `/api/sprints/${id}/board`;
  });

  readonly value = computed(() => this.board.value());
  readonly isLoading = this.board.isLoading;
  readonly error = this.board.error;

  /** Edit-drawer target: which column, and whether it's scoped to the whole
   * column, just the sprint goal, or a single task (null = drawer closed). */
  readonly drawer = signal<{ projectId: number; scope: 'column' | 'goal' | 'task'; taskId?: number } | null>(null);

  readonly openColumnId = computed(() => this.drawer()?.projectId ?? null);
  readonly drawerScope = computed(() => this.drawer()?.scope ?? 'column');
  readonly openColumn = computed(() =>
    this.board.value()?.columns.find((c) => c.projectId === this.openColumnId()) ?? null,
  );
  readonly openTask = computed(() => {
    const id = this.drawer()?.taskId;
    return id == null ? null : (this.openColumn()?.tasks.find((t) => t.id === id) ?? null);
  });

  openColumnDrawer(projectId: number): void {
    this.drawer.set({ projectId, scope: 'column' });
  }
  openGoalDrawer(projectId: number): void {
    this.drawer.set({ projectId, scope: 'goal' });
  }
  openTaskDrawer(projectId: number, taskId: number): void {
    this.drawer.set({ projectId, scope: 'task', taskId });
  }
  closeDrawer(): void {
    this.drawer.set(null);
  }

  /** Resolve the current-or-next sprint and select it. */
  async loadCurrent(): Promise<void> {
    const sprint = await firstValueFrom(this.http.get<Sprint>('/api/sprints/current'));
    this.sprintId.set(sprint.id);
  }

  goto(sprint: Sprint | null): void {
    if (sprint) this.sprintId.set(sprint.id);
  }

  // --- writes (optimistic + reconcile) ---

  async setGoal(col: BoardColumn, goalText: string, status: GoalStatus, notes: string | null = col.notes): Promise<void> {
    const sprintId = this.sprintId();
    if (sprintId == null) return;
    this.patchColumn(col.projectId, (c) => ({ ...c, goalText, status, notes }));
    await this.send(() =>
      this.http.put(`/api/sprints/${sprintId}/projects/${col.projectId}/goal`, { goalText, notes, status }),
    );
  }

  async setStatus(col: BoardColumn, status: GoalStatus): Promise<void> {
    await this.setGoal(col, col.goalText ?? '', status, col.notes);
  }

  async addTask(col: BoardColumn, label: string, points: number, priority: TaskPriority | null): Promise<void> {
    const sprintId = this.sprintId();
    if (sprintId == null) return;
    const temp: TaskItem = {
      id: -Date.now(),
      label,
      description: null,
      isDone: false,
      points,
      priority,
      sortOrder: col.tasks.length,
      assigneeId: null,
      assigneeName: null,
    };
    this.patchColumn(col.projectId, (c) => ({ ...c, tasks: [...c.tasks, temp], points: c.points + points }));
    await this.send(() =>
      this.http.post(`/api/sprints/${sprintId}/projects/${col.projectId}/tasks`, { label, points, priority }),
    );
  }

  async toggleTask(projectId: number, task: TaskItem): Promise<void> {
    this.patchColumn(projectId, (c) => ({
      ...c,
      tasks: c.tasks.map((t) => (t.id === task.id ? { ...t, isDone: !t.isDone } : t)),
    }));
    await this.send(() => this.http.patch(`/api/tasks/${task.id}`, { isDone: !task.isDone }));
  }

  async updateTask(projectId: number, task: TaskItem, patch: Partial<Pick<TaskItem, 'label' | 'points' | 'priority' | 'description'>>): Promise<void> {
    this.patchColumn(projectId, (c) => ({
      ...c,
      tasks: c.tasks.map((t) => (t.id === task.id ? { ...t, ...patch } : t)),
    }));
    await this.send(() => this.http.patch(`/api/tasks/${task.id}`, patch));
  }

  async setPriority(projectId: number, task: TaskItem, priority: TaskPriority | null): Promise<void> {
    this.patchColumn(projectId, (c) => ({
      ...c,
      tasks: c.tasks.map((t) => (t.id === task.id ? { ...t, priority } : t)),
    }));
    await this.send(() => this.http.patch(`/api/tasks/${task.id}/priority`, { priority }));
  }

  async setAssignee(projectId: number, task: TaskItem, assigneeId: string | null): Promise<void> {
    const assigneeName =
      assigneeId == null
        ? null
        : (this.board.value()?.capacity.find((u) => u.userId === assigneeId)?.displayName ?? null);
    this.patchColumn(projectId, (c) => ({
      ...c,
      tasks: c.tasks.map((t) => (t.id === task.id ? { ...t, assigneeId, assigneeName } : t)),
    }));
    await this.send(() => this.http.patch(`/api/tasks/${task.id}/assignee`, { assigneeId }));
  }

  async deleteTask(projectId: number, task: TaskItem): Promise<void> {
    this.patchColumn(projectId, (c) => ({
      ...c,
      tasks: c.tasks.filter((t) => t.id !== task.id),
      points: c.points - task.points,
    }));
    await this.send(() => this.http.delete(`/api/tasks/${task.id}`));
  }

  /** Apply an optimistic transform to one project's column in the local board. */
  private patchColumn(projectId: number, fn: (c: BoardColumn) => BoardColumn): void {
    const current = this.board.value();
    if (!current) return;
    const columns = current.columns.map((c) => (c.projectId === projectId ? fn(c) : c));
    const totalPoints = columns.reduce((sum, c) => sum + c.points, 0);
    this.board.value.set({
      ...current,
      columns,
      totalPoints,
      isOverloaded: totalPoints > current.teamCapacity,
    });
  }

  /** Fire a write; always reload() afterward so the server's totals win. */
  private async send(call: () => { subscribe: unknown } | any): Promise<void> {
    try {
      await firstValueFrom(call());
    } finally {
      this.board.reload();
    }
  }
}
