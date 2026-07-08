import { Injectable, computed, inject } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { CreateUserPayload, UpdateUserPayload, User } from './models';

/**
 * Signal store for team (user) management. Reads use httpResource; writes call
 * the API then reload() so the list reflects the server. Admin-only endpoints.
 */
@Injectable({ providedIn: 'root' })
export class TeamStore {
  private readonly http = inject(HttpClient);

  readonly users = httpResource<User[]>(() => '/api/users');

  readonly value = computed(() => this.users.value() ?? []);
  readonly isLoading = this.users.isLoading;
  readonly error = this.users.error;

  readonly adminCount = computed(() => this.value().filter((u) => u.isAdmin).length);

  async create(payload: CreateUserPayload): Promise<void> {
    await firstValueFrom(this.http.post('/api/users', payload));
    this.users.reload();
  }

  async update(id: string, patch: UpdateUserPayload): Promise<void> {
    await firstValueFrom(this.http.patch(`/api/users/${id}`, patch));
    this.users.reload();
  }

  async resetPassword(id: string, password: string): Promise<void> {
    await firstValueFrom(this.http.post(`/api/users/${id}/password`, { password }));
  }

  async remove(id: string): Promise<void> {
    await firstValueFrom(this.http.delete(`/api/users/${id}`));
    this.users.reload();
  }
}
