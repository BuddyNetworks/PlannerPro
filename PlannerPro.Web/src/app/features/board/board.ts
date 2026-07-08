import { Component, computed, inject } from '@angular/core';
import { BoardStore } from '../../core/board-store';
import { BoardColumnComponent } from './board-column';

@Component({
  selector: 'app-board',
  imports: [BoardColumnComponent],
  templateUrl: './board.html',
  styleUrl: './board.scss',
})
export class BoardView {
  readonly store = inject(BoardStore);
  readonly board = this.store.value;
  readonly loading = this.store.isLoading;

  /** Fraction of the overload threshold currently used, clamped to [0,1]. */
  readonly effortFraction = computed(() => {
    const b = this.board();
    if (!b) return 0;
    return Math.min(1, b.totalPoints / b.overloadThreshold);
  });

  constructor() {
    if (this.store.sprintId() == null) {
      this.store.loadCurrent();
    }
  }

  formatRange(startIso: string, endIso: string): string {
    const opts: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric' };
    const s = new Date(startIso + 'T00:00:00').toLocaleDateString(undefined, opts);
    const e = new Date(endIso + 'T00:00:00').toLocaleDateString(undefined, {
      ...opts,
      year: 'numeric',
    });
    return `${s} – ${e}`;
  }
}
