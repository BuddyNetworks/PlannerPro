import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

/**
 * Sends the auth cookie on every request (same-origin via the dev proxy) and,
 * on a 401 that isn't the login/me probe itself, bounces to the login page.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const withCreds = req.clone({ withCredentials: true });

  return next(withCreds).pipe(
    catchError((err) => {
      const isAuthProbe = req.url.includes('/api/auth/');
      if (err?.status === 401 && !isAuthProbe) {
        router.navigate(['/login']);
      }
      return throwError(() => err);
    }),
  );
};
