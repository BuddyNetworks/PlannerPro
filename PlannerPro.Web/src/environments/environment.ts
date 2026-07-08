// Default (development) environment. `ng serve` uses this file.
export const environment = {
  production: false,
  // Base URL prepended to every /api request by apiBaseInterceptor.
  // '' = same-origin (relative paths) — what the dev proxy (proxy.conf.js) and
  // the AKS ingress both expect. Only set an absolute URL to target a remote
  // API on a *different* origin (which also requires CORS + SameSite=None).
  apiBaseUrl: '',
};
