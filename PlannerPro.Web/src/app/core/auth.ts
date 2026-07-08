import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

interface MeResponse {
  email: string;
}

@Injectable({ providedIn: 'root' })
export class Auth {
  private readonly http = inject(HttpClient);

  readonly user = signal<string | null>(null);
  readonly isAuthenticated = signal(false);
  /** True until the initial /me probe resolves, so guards can wait. */
  readonly ready = signal(false);

  /** Called once at startup to learn whether an auth cookie is already valid. */
  async probe(): Promise<boolean> {
    try {
      const me = await firstValueFrom(this.http.get<MeResponse>('/api/auth/me'));
      this.setUser(me.email);
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
      this.setUser(email);
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

  private setUser(email: string) {
    this.user.set(email);
    this.isAuthenticated.set(true);
  }

  private clear() {
    this.user.set(null);
    this.isAuthenticated.set(false);
  }
}
