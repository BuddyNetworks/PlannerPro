import { HttpInterceptorFn } from '@angular/common/http';
import { environment } from '../../environments/environment';

/**
 * Prefixes the configured API base URL onto app-relative /api requests, so the
 * exact same store/service code targets the dev proxy, a bundled same-origin
 * API, or a remote API purely via environment config.
 *
 * No-op when apiBaseUrl is '' (the default) — requests stay relative and hit
 * whatever origin served the page, which is what the AKS ingress relies on.
 */
export const apiBaseInterceptor: HttpInterceptorFn = (req, next) => {
  const base = environment.apiBaseUrl;
  if (base && req.url.startsWith('/api')) {
    return next(req.clone({ url: base.replace(/\/$/, '') + req.url }));
  }
  return next(req);
};
