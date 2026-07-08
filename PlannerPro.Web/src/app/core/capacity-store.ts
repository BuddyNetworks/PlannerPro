import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { CapacityMatrix } from './models';

/**
 * Signal store for the capacity-planning matrix (users × a window of sprints).
 * Reads use httpResource keyed on the sprint window; writing an override calls
 * the API then reload()s so recomputed load/over flags come from the server.
 */
@Injectable({ providedIn: 'root' })
export class CapacityStore {
  private readonly http = inject(HttpClient);

  /** First sprint number of the window; null lets the API default to current. */
  readonly fromSprint = signal<number | null>(null);
  readonly count = signal(6);

  readonly matrix = httpResource<CapacityMatrix>(() => {
    const params = new URLSearchParams();
    const from = this.fromSprint();
    if (from != null) params.set('fromSprint', String(from));
    params.set('count', String(this.count()));
    return `/api/capacity?${params.toString()}`;
  });

  readonly value = computed(() => this.matrix.value());
  readonly isLoading = this.matrix.isLoading;
  readonly error = this.matrix.error;

  next(): void {
    const sprints = this.value()?.sprints;
    if (sprints?.length) this.fromSprint.set(sprints[0].number + this.count());
  }

  prev(): void {
    const sprints = this.value()?.sprints;
    if (sprints?.length) this.fromSprint.set(Math.max(1, sprints[0].number - this.count()));
  }

  /** Set (points) or clear (null) a per-sprint capacity override. */
  async setOverride(sprintId: number, userId: string, points: number | null): Promise<void> {
    await firstValueFrom(
      this.http.put(`/api/sprints/${sprintId}/users/${userId}/capacity`, { points }),
    );
    this.matrix.reload();
  }
}
