import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

interface MeResponse {
  email: string;
  displayName: string;
  isAdmin: boolean;
}

@Injectable({ providedIn: 'root' })
export class Auth {
  private readonly http = inject(HttpClient);

  readonly user = signal<string | null>(null);
  readonly displayName = signal<string | null>(null);
  readonly isAdmin = signal(false);
  readonly isAuthenticated = signal(false);
  /** True until the initial /me probe resolves, so guards can wait. */
  readonly ready = signal(false);

  /** Called once at startup to learn whether an auth cookie is already valid. */
  async probe(): Promise<boolean> {
    try {
      await this.fetchMe();
    } catch {
      this.clear();
    } finally {
      this.ready.set(true);
    }
    return this.isAuthenticated();
  }

  async login(email: string, password: string): Promise<boolean> {
    try {
      await firstValueFrom(this.http.post('/api/auth/login', { email, password }));
      // The login response carries only the email; fetch /me for role + name.
      await this.fetchMe();
      return true;
    } catch {
      this.clear();
      return false;
    }
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/auth/logout', {}));
    } finally {
      this.clear();
    }
  }

  private async fetchMe(): Promise<void> {
    const me = await firstValueFrom(this.http.get<MeResponse>('/api/auth/me'));
    this.user.set(me.email);
    this.displayName.set(me.displayName);
    this.isAdmin.set(me.isAdmin);
    this.isAuthenticated.set(true);
  }

  private clear() {
    this.user.set(null);
    this.displayName.set(null);
    this.isAdmin.set(false);
    this.isAuthenticated.set(false);
  }
}
