var builder = DistributedApplication.CreateBuilder(args);

// SQL Server as an Aspire-managed container with a persistent data volume
// so PlannerPro data survives container restarts in development.
var sql = builder.AddSqlServer("sql")
    .WithDataVolume("plannerpro-sql-data")
    .WithLifetime(ContainerLifetime.Persistent);

var db = sql.AddDatabase("plannerdb");

// .NET 10 Web API. Connection string for "plannerdb" is injected as
// ConnectionStrings__plannerdb; API waits for SQL Server to be healthy first.
var api = builder.AddProject<Projects.PlannerPro_Api>("api")
    .WithReference(db)
    .WaitFor(db);

// Angular 22 front end, run as an npm app (zoneless, Vite-based dev server).
// The API endpoint URL is injected via WithReference so the dev proxy can
// forward /api calls to it. Fixed port 4200 keeps the proxy config simple.
builder.AddNpmApp("web", "../PlannerPro.Web", "start")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: 4200, targetPort: 4200, isProxied: false)
    .WithExternalHttpEndpoints()
    .WithEnvironment("BROWSER", "none");

builder.Build().Run();
