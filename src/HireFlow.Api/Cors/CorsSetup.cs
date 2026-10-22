namespace HireFlow.Api.Cors;

/// <summary>
/// Lets browser front ends on other origins (e.g. the HireFlow web app) call the API.
/// Origins come from <c>Cors:AllowedOrigins</c>; with none configured, cross-origin calls stay blocked.
/// </summary>
public static class CorsSetup
{
    public const string PolicyName = "HireFlowWeb";

    public static IServiceCollection AddHireFlowCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options => options.AddPolicy(PolicyName, policy =>
        {
            if (origins.Length == 0)
            {
                return;
            }

            policy.WithOrigins(origins)
                .WithMethods("GET", "POST")
                .WithHeaders("Authorization", "Content-Type", "GraphQL-Preflight")
                .SetPreflightMaxAge(TimeSpan.FromHours(1));
        }));

        return services;
    }
}
