import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Auth } from '../../core/auth';
import { TeamStore } from '../../core/team-store';
import { User } from '../../core/models';

@Component({
  selector: 'app-team',
  imports: [RouterLink],
  templateUrl: './team.html',
  styleUrl: './team.scss',
})
export class TeamView {
  protected readonly store = inject(TeamStore);
  protected readonly auth = inject(Auth);

  // Identify the signed-in user so the UI can prevent self-delete/self-demote.
  readonly isSelf = (u: User) => u.email === this.auth.user();

  // --- add user form ---
  readonly showAdd = signal(false);
  readonly nEmail = signal('');
  readonly nName = signal('');
  readonly nPassword = signal('');
  readonly nAdmin = signal(false);
  readonly nCapacity = signal(24);
  readonly addError = signal<string | null>(null);
  readonly busy = signal(false);

  toggleAdd() {
    this.showAdd.update((v) => !v);
    this.addError.set(null);
  }

  async add() {
    this.addError.set(null);
    const email = this.nEmail().trim();
    const displayName = this.nName().trim();
    if (!email || !displayName || this.nPassword().length < 8) {
      this.addError.set('Email, display name, and an 8+ character password are required.');
      return;
    }
    this.busy.set(true);
    try {
      await this.store.create({
        email,
        displayName,
        password: this.nPassword(),
        isAdmin: this.nAdmin(),
        defaultCapacityPoints: this.nCapacity(),
      });
      this.nEmail.set('');
      this.nName.set('');
      this.nPassword.set('');
      this.nAdmin.set(false);
      this.nCapacity.set(24);
      this.showAdd.set(false);
    } catch (e) {
      this.addError.set(this.msg(e));
    } finally {
      this.busy.set(false);
    }
  }

  // --- inline edit ---
  readonly editingId = signal<string | null>(null);
  readonly eName = signal('');
  readonly eCapacity = signal(24);
  readonly eAdmin = signal(false);
  readonly rowError = signal<{ id: string; msg: string } | null>(null);

  startEdit(u: User) {
    this.editingId.set(u.id);
    this.eName.set(u.displayName);
    this.eCapacity.set(u.defaultCapacityPoints);
    this.eAdmin.set(u.isAdmin);
    this.rowError.set(null);
  }

  cancelEdit() {
    this.editingId.set(null);
  }

  async saveEdit(u: User) {
    this.busy.set(true);
    try {
      await this.store.update(u.id, {
        displayName: this.eName().trim() || u.displayName,
        defaultCapacityPoints: this.eCapacity(),
        isAdmin: this.eAdmin(),
      });
      this.editingId.set(null);
      this.rowError.set(null);
    } catch (e) {
      this.rowError.set({ id: u.id, msg: this.msg(e) });
    } finally {
      this.busy.set(false);
    }
  }

  // --- reset password ---
  readonly resettingId = signal<string | null>(null);
  readonly resetPw = signal('');
  readonly resetDone = signal<string | null>(null);

  startReset(u: User) {
    this.resettingId.set(u.id);
    this.resetPw.set('');
    this.resetDone.set(null);
    this.rowError.set(null);
  }

  cancelReset() {
    this.resettingId.set(null);
  }

  async saveReset(u: User) {
    if (this.resetPw().length < 8) {
      this.rowError.set({ id: u.id, msg: 'Password must be at least 8 characters.' });
      return;
    }
    this.busy.set(true);
    try {
      await this.store.resetPassword(u.id, this.resetPw());
      this.resettingId.set(null);
      this.resetDone.set(u.id);
    } catch (e) {
      this.rowError.set({ id: u.id, msg: this.msg(e) });
    } finally {
      this.busy.set(false);
    }
  }

  // --- delete ---
  readonly confirmingId = signal<string | null>(null);

  askDelete(u: User) {
    this.confirmingId.set(u.id);
    this.rowError.set(null);
  }

  cancelDelete() {
    this.confirmingId.set(null);
  }

  async remove(u: User) {
    this.busy.set(true);
    try {
      await this.store.remove(u.id);
      this.confirmingId.set(null);
    } catch (e) {
      this.rowError.set({ id: u.id, msg: this.msg(e) });
    } finally {
      this.busy.set(false);
    }
  }

  /** True when removing/demoting this user would drop the last admin. */
  readonly lastAdmin = (u: User) => u.isAdmin && this.store.adminCount() <= 1;

  private msg(e: unknown): string {
    const err = (e as { error?: unknown })?.error;
    if (typeof err === 'string') return err;
    const obj = err as { detail?: string; title?: string } | null;
    return obj?.detail ?? obj?.title ?? 'Something went wrong. Please try again.';
  }
}
