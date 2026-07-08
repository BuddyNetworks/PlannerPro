import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Auth } from './auth';

/** Guards admin-only routes: waits for the startup probe, requires an admin.
 * Non-admins are sent to the board; unauthenticated users to login. */
export const adminGuard: CanActivateFn = async () => {
  const auth = inject(Auth);
  const router = inject(Router);

  if (!auth.ready()) {
    await auth.probe();
  }
  if (!auth.isAuthenticated()) return router.createUrlTree(['/login']);
  if (auth.isAdmin()) return true;

  return router.createUrlTree(['/board']);
};
