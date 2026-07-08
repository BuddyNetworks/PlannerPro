// Production environment. Swapped in for environment.ts by the `production`
// build configuration (see angular.json → fileReplacements).
export const environment = {
  production: true,
  // Same-origin by default: in AKS the ingress routes /api to the plannerproapi
  // service, so the browser only ever talks to one host and the auth cookie
  // works unchanged. To point the SPA at an absolute API origin instead, set
  // e.g. 'https://plannerpro-api.dev.buddynetworks.net' here and rebuild the
  // web image — but a cross-origin API also needs CORS enabled and the auth
  // cookies switched to SameSite=None in the API.
  apiBaseUrl: 'https://plannerapi.buddynetworks.net',
};
