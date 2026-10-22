using System.Text.Json;
using HireFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HireFlow.Api.Health;

/// <summary>
/// Kubernetes-style probes: <c>/health/live</c> only says the process is up, while
/// <c>/health/ready</c> also checks that the database answers.
/// </summary>
public static class HealthEndpoints
{
    private const string ReadyTag = "ready";

    public static IServiceCollection AddHireFlowHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<HireFlowDbContext>("database", tags: [ReadyTag]);
        return services;
    }

    public static IEndpointRouteBuilder MapHireFlowHealthChecks(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteJsonAsync,
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteJsonAsync,
        });

        // Backwards-compatible alias for the original endpoint.
        app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteJsonAsync });
        return app;
    }

    private static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return JsonSerializer.SerializeAsync(context.Response.Body, new
        {
            status = report.Status.ToString(),
            durationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                error = e.Value.Exception?.Message,
            }),
        }, cancellationToken: context.RequestAborted);
    }
}
