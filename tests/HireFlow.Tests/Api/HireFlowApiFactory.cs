using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HireFlow.Tests.Api;

/// <summary>
/// Boots the real API against a throw-away database seeded with demo data.
/// Uses a temporary SQLite file by default; set <c>HIREFLOW_TEST_POSTGRES</c> to a server
/// connection string (without a database name) to run the same suite against PostgreSQL.
/// </summary>
public sealed class HireFlowApiFactory : WebApplicationFactory<Program>
{
    private static readonly string? PostgresServer = Environment.GetEnvironmentVariable("HIREFLOW_TEST_POSTGRES");

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"hireflow-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Database:Seed", "true");

        if (string.IsNullOrEmpty(PostgresServer))
        {
            builder.UseSetting("Database:Provider", "Sqlite");
            builder.UseSetting("ConnectionStrings:HireFlow", $"Data Source={_databasePath};Pooling=False");
        }
        else
        {
            builder.UseSetting("Database:Provider", "Postgres");
            builder.UseSetting("ConnectionStrings:HireFlow", $"{PostgresServer};Database=hireflow_test_{Guid.NewGuid():N}");
        }
    }

    public async Task<GraphQLClient> CreateGraphQLClientAsync(string? role = null)
    {
        var http = CreateClient();
        if (role is not null)
        {
            var response = await http.PostAsJsonAsync("/auth/token", new { name = "Test Recruiter", role });
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        }

        return new GraphQLClient(http);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}

public sealed class GraphQLClient(HttpClient http)
{
    public HttpClient Http { get; } = http;

    public async Task<JsonElement> ExecuteAsync(string query, object? variables = null)
    {
        var response = await Http.PostAsJsonAsync("/graphql", new { query, variables });
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}

public static class JsonElementExtensions
{
    /// <summary>Walks a dotted path such as <c>data.jobs.nodes</c>.</summary>
    public static JsonElement At(this JsonElement element, string path)
    {
        foreach (var segment in path.Split('.'))
        {
            element = int.TryParse(segment, out var index) ? element[index] : element.GetProperty(segment);
        }

        return element;
    }

    public static bool HasErrors(this JsonElement result) => result.TryGetProperty("errors", out _);
}
