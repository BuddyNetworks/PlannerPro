using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlannerPro.Api.Api;
using PlannerPro.Api.Data;
using PlannerPro.Api.Domain;

var builder = WebApplication.CreateBuilder(args);

// Aspire wiring: health checks, OpenTelemetry, service discovery, resilience.
builder.AddServiceDefaults();

// EF Core context bound to the "plannerdb" connection string. Locally Aspire
// injects it (ConnectionStrings__plannerdb); in AKS it comes from the
// plannerpro-secrets Secret mounted as the same environment variable.
builder.AddSqlServerDbContext<PlannerDbContext>("plannerdb");

// Behind the AKS ingress, TLS terminates at the edge and traffic reaches the
// container over plain HTTP. Honor X-Forwarded-Proto/For so Request.IsHttps
// reflects the *original* request. Without this the Secure auth cookie is
// dropped and UseHttpsRedirection bounces an already-HTTPS request into a loop.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // The ingress is the only proxy in front of us; trust it without pinning IPs.
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// Single-user auth: ASP.NET Core Identity with a cookie for the same-origin SPA.
builder.Services
    .AddIdentityCore<ApplicationUser>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<PlannerDbContext>()
    // Registers the "Default" DataProtector token provider used by admin
    // password resets (GeneratePasswordResetTokenAsync / ResetPasswordAsync).
    .AddDefaultTokenProviders()
    .AddSignInManager();

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, o =>
    {
        o.Cookie.Name = "PlannerPro.Auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
        // API-style: return status codes instead of redirecting to a login page.
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });

builder.Services.AddAuthorization();

// Antiforgery for the SPA: Angular reads the XSRF-TOKEN cookie and echoes it in
// the X-XSRF-TOKEN header on mutations; the API validates it (see AntiforgeryFilter).
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-XSRF-TOKEN";
    o.Cookie.Name = "PlannerPro.Antiforgery";
    // Secure under https (production) without throwing on a plain-http request.
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Serialize enums as strings (e.g. "InProgress") for a friendlier SPA contract.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// Apply migrations and seed on startup (single-user internal tool).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PlannerDbContext>();
    await db.Database.MigrateAsync();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
    await DbSeeder.SeedAsync(scope.ServiceProvider, app.Configuration, logger);
}

// Must run before HTTPS redirection / cookie issuance so the ingress-forwarded
// scheme is applied to the request first.
app.UseForwardedHeaders();

// Aspire default endpoints: /health and /alive (Development only by default).
// In AKS the Kubernetes probes hit the anonymous /api/ping instead.
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// In Production the API serves the built Angular app (same origin → the auth
// cookie just works). In Development the Angular dev server + proxy handles this.
if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();

// Issue a JS-readable antiforgery token cookie on page/API GETs (not on every
// static asset) so the SPA can echo it back on state-changing requests.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    var isApi = context.Request.Path.StartsWithSegments("/api");
    var isPage = !Path.HasExtension(path); // SPA routes and "/" have no extension
    if (HttpMethods.IsGet(context.Request.Method) && (isApi || isPage))
    {
        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
        });
    }
    await next();
});

app.MapControllers();

// Planner data API (sprints, board, goals, tasks) — requires auth.
app.MapPlannerApi();

// Team & capacity management API (users + capacity); writes are admin-guarded.
app.MapTeamApi();

// --- Auth endpoints ---
var auth = app.MapGroup("/api/auth");

auth.MapPost("/login", async (LoginRequest req, SignInManager<ApplicationUser> signIn) =>
{
    var result = await signIn.PasswordSignInAsync(req.Email, req.Password, isPersistent: true, lockoutOnFailure: false);
    return result.Succeeded
        ? Results.Ok(new { email = req.Email })
        : Results.Unauthorized();
});

auth.MapPost("/logout", async (SignInManager<ApplicationUser> signIn) =>
{
    await signIn.SignOutAsync();
    return Results.Ok();
});

// Returns the signed-in user's identity plus admin flag + display name so the
// SPA can gate the management UI. Looks up the DB record (admin can change).
auth.MapGet("/me", async (HttpContext ctx, UserManager<ApplicationUser> users) =>
{
    if (ctx.User.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();
    var user = await users.GetUserAsync(ctx.User);
    return user is null
        ? Results.Unauthorized()
        : Results.Ok(new { email = user.Email, displayName = user.DisplayName, isAdmin = user.IsAdmin });
});

app.MapGet("/api/ping", () => Results.Ok(new
{
    status = "ok",
    service = "PlannerPro.Api",
    utc = DateTimeOffset.UtcNow
}));

// SPA fallback (Production): any non-API route returns index.html so Angular's
// client-side router can handle it.
if (!app.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html");
}

app.Run();

internal record LoginRequest(string Email, string Password);
