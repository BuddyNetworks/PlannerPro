// Dev-server proxy: forward /api/* to the PlannerPro API.
// Under `aspire run`, Aspire injects the API's address via WithReference as
// services__api__https__0 / services__api__http__0. Falls back to the API's
// fixed launchSettings https port when running `ng serve` standalone.
const target =
  process.env['services__api__https__0'] ||
  process.env['services__api__http__0'] ||
  'https://localhost:7265';

module.exports = {
  '/api': {
    target,
    secure: false,
    changeOrigin: true,
  },
};
