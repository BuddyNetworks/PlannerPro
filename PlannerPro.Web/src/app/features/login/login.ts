import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Auth } from '../../core/auth';

@Component({
  selector: 'app-login',
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);

  readonly email = signal('');
  readonly password = signal('');
  readonly error = signal<string | null>(null);
  readonly submitting = signal(false);

  async submit(event: Event) {
    event.preventDefault();
    if (this.submitting()) return;
    this.error.set(null);
    this.submitting.set(true);

    const ok = await this.auth.login(this.email().trim(), this.password());
    this.submitting.set(false);

    if (ok) {
      this.router.navigateByUrl('/board');
    } else {
      this.error.set('Invalid email or password.');
    }
  }
}
