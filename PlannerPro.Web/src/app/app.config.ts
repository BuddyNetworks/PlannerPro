import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors, withXsrfConfiguration } from '@angular/common/http';

import { routes } from './app.routes';
import { apiBaseInterceptor } from './core/api-base.interceptor';
import { authInterceptor } from './core/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(
      // apiBaseInterceptor rewrites the URL first (so a configured absolute API
      // base applies), then authInterceptor attaches credentials / handles 401s.
      withInterceptors([apiBaseInterceptor, authInterceptor]),
      // Angular reads this cookie and sends the header on mutating requests;
      // the API validates it (matches AddAntiforgery on the server).
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
    ),
  ],
};
