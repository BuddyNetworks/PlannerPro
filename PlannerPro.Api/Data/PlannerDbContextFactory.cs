using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PlannerPro.Api.Data;

/// <summary>Design-time factory used ONLY by `dotnet ef migrations`. It builds
/// the model without needing Aspire's injected connection string or a live DB.
/// At runtime the context is configured by AddSqlServerDbContext in Program.cs.</summary>
public class PlannerDbContextFactory : IDesignTimeDbContextFactory<PlannerDbContext>
{
    public PlannerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PlannerDbContext>()
            .UseSqlServer("Server=localhost;Database=plannerdb;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new PlannerDbContext(options);
    }
}
