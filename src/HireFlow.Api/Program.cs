using HireFlow.Api.Auth;
using HireFlow.Api.Cors;
using HireFlow.Api.Health;
using HireFlow.Infrastructure;
using HireFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddInfrastructure(builder.Configuration)
    .AddHireFlowAuth(builder.Configuration)
    .AddHireFlowCors(builder.Configuration)
    .AddHireFlowHealthChecks()
    .AddSingleton(TimeProvider.System);

builder.Services
    .AddGraphQLServer()
    .AddAuthorization()
    .AddApiTypes()
    .AddFiltering()
    .AddSorting()
    .AddMutationConventions(applyToAllMutations: true)
    .AddInMemorySubscriptions()
    .AddSocketSessionInterceptor<SocketAuthInterceptor>()
    .AddMaxExecutionDepthRule(12)
    .ModifyPagingOptions(o =>
    {
        o.DefaultPageSize = 10;
        o.MaxPageSize = 50;
        o.IncludeTotalCount = true;
    })
    .ModifyCostOptions(o =>
    {
        // Filters and sort orders sent as variables are priced at their worst case, multiplied
        // by this factor (default 5). That pushed an ordinary filtered job board query to ~4000
        // against the 1000 limit, so well-behaved clients using variables were rejected.
        // A factor of 1 still prices variables at their worst case, without the extra penalty.
        o.Filtering.VariableMultiplier = 1;
        o.Sorting.VariableMultiplier = 1;

        // Budget sized for the web app's heaviest screen (a filtered 12-card job board is about
        // 1050). Abusive fan-out queries still cost tens of thousands and stay rejected
        // (see CostAnalysisTests).
        o.MaxFieldCost = 2_000;
    })
    .ModifyRequestOptions(o => o.IncludeExceptionDetails = builder.Environment.IsDevelopment());

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider
        .GetRequiredService<DatabaseInitializer>()
        .InitializeAsync(seed: app.Configuration.GetValue("Database:Seed", true));
}

app.UseWebSockets();
app.UseCors(CorsSetup.PolicyName);
app.UseAuthentication();
app.UseAuthorization();

app.MapGraphQL();
app.MapDevTokenEndpoint();
app.MapHireFlowHealthChecks();
app.MapGet("/", () => Results.Redirect("/graphql")).ExcludeFromDescription();

await app.RunAsync();

public partial class Program;
