import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Auth } from './auth';

/** Guards app routes: waits for the startup probe, then requires auth. */
export const authGuard: CanActivateFn = async () => {
  const auth = inject(Auth);
  const router = inject(Router);

  if (!auth.ready()) {
    await auth.probe();
  }
  if (auth.isAuthenticated()) return true;

  return router.createUrlTree(['/login']);
};
