import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CapacityStore } from '../../core/capacity-store';
import { CapacityCell, CapacityRow } from '../../core/models';

@Component({
  selector: 'app-capacity',
  imports: [RouterLink],
  templateUrl: './capacity.html',
  styleUrl: './capacity.scss',
})
export class CapacityView {
  protected readonly store = inject(CapacityStore);

  readonly editingKey = signal<string | null>(null);
  readonly draft = signal(0);

  key(userId: string, sprintId: number): string {
    return `${userId}:${sprintId}`;
  }

  cellClass(cell: CapacityCell): string {
    if (cell.capacity === 0) return cell.assignedPoints > 0 ? 'over' : 'empty';
    if (cell.isOver) return 'over';
    if (cell.assignedPoints >= cell.capacity * 0.8) return 'near';
    if (cell.assignedPoints === 0) return 'empty';
    return 'ok';
  }

  startEdit(row: CapacityRow, cell: CapacityCell): void {
    this.editingKey.set(this.key(row.user.id, cell.sprintId));
    this.draft.set(cell.capacity);
  }

  cancel(): void {
    this.editingKey.set(null);
  }

  async save(row: CapacityRow, cell: CapacityCell): Promise<void> {
    const pts = Math.max(0, Math.min(200, this.draft()));
    this.editingKey.set(null);
    await this.store.setOverride(cell.sprintId, row.user.id, pts);
  }

  async clearOverride(row: CapacityRow, cell: CapacityCell): Promise<void> {
    this.editingKey.set(null);
    await this.store.setOverride(cell.sprintId, row.user.id, null);
  }

  shortRange(startIso: string, endIso: string): string {
    const o: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric' };
    const s = new Date(startIso + 'T00:00:00').toLocaleDateString(undefined, o);
    const e = new Date(endIso + 'T00:00:00').toLocaleDateString(undefined, o);
    return `${s} – ${e}`;
  }
}
