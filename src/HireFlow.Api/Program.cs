using HireFlow.Api.Auth;
using HireFlow.Api.Health;
using HireFlow.Infrastructure;
using HireFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddInfrastructure(builder.Configuration)
    .AddHireFlowAuth(builder.Configuration)
    .AddHireFlowHealthChecks();

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
    .ModifyRequestOptions(o => o.IncludeExceptionDetails = builder.Environment.IsDevelopment());

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider
        .GetRequiredService<DatabaseInitializer>()
        .InitializeAsync(seed: app.Configuration.GetValue("Database:Seed", true));
}

app.UseWebSockets();
app.UseAuthentication();
app.UseAuthorization();

app.MapGraphQL();
app.MapDevTokenEndpoint();
app.MapHireFlowHealthChecks();
app.MapGet("/", () => Results.Redirect("/graphql")).ExcludeFromDescription();

await app.RunAsync();

public partial class Program;
