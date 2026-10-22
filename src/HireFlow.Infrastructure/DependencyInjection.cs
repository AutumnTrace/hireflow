using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence. <c>Database:Provider</c> selects <c>Sqlite</c> (default, zero setup)
    /// or <c>Postgres</c>; the connection string is read from <c>ConnectionStrings:HireFlow</c>.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("HireFlow") ?? "Data Source=hireflow.db";

        services.AddDbContextPool<HireFlowDbContext>(options =>
        {
            _ = provider.ToUpperInvariant() switch
            {
                "SQLITE" => options.UseSqlite(connectionString),
                "POSTGRES" or "POSTGRESQL" => options.UseNpgsql(connectionString),
                _ => throw new InvalidOperationException($"Unsupported database provider '{provider}'. Use 'Sqlite' or 'Postgres'."),
            };
        });

        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
