import { Component, computed, inject } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { Router } from '@angular/router';
import { BoardStore } from '../../core/board-store';
import { GoalStatus, STATUS_LABEL, Timeline, TimelineRow } from '../../core/models';

@Component({
  selector: 'app-timeline',
  templateUrl: './timeline.html',
  styleUrl: './timeline.scss',
})
export class TimelineView {
  private readonly store = inject(BoardStore);
  private readonly router = inject(Router);

  readonly statusLabel = STATUS_LABEL;

  readonly timeline = httpResource<Timeline>(() => '/api/timeline');
  readonly data = computed(() => this.timeline.value());
  readonly loading = this.timeline.isLoading;

  /** Sprint number of the current-or-next sprint, for row highlighting. */
  readonly currentNumber = computed(() => {
    const rows = this.data()?.rows;
    if (!rows?.length) return null;
    const today = new Date().toISOString().slice(0, 10);
    const containing = rows.find((r) => r.sprint.startDate <= today && today <= r.sprint.endDate);
    if (containing) return containing.sprint.number;
    const upcoming = rows.find((r) => r.sprint.startDate > today);
    return (upcoming ?? rows[rows.length - 1]).sprint.number;
  });

  statusClass(status: GoalStatus | null): string {
    return 'st-' + (status ?? 'none').toLowerCase();
  }

  openBoard(row: TimelineRow): void {
    this.store.sprintId.set(row.sprint.id);
    this.router.navigateByUrl('/board');
  }

  shortRange(startIso: string, endIso: string): string {
    const o: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric' };
    const s = new Date(startIso + 'T00:00:00').toLocaleDateString(undefined, o);
    const e = new Date(endIso + 'T00:00:00').toLocaleDateString(undefined, o);
    return `${s} – ${e}`;
  }

  year(startIso: string): string {
    return startIso.slice(0, 4);
  }
}
