namespace HireFlow.Tests.Api;

/// <summary>
/// Guards the public contract: any schema change shows up as a diff of <c>schema.graphql</c>
/// in code review. Run with <c>UPDATE_SCHEMA=1</c> to accept an intentional change.
/// </summary>
public sealed class SchemaSnapshotTests(HireFlowApiFactory factory) : IClassFixture<HireFlowApiFactory>
{
    [Fact]
    public async Task Schema_matches_committed_snapshot()
    {
        var client = await factory.CreateGraphQLClientAsync();
        var actual = Normalize(await client.Http.GetStringAsync("/graphql?sdl"));
        var snapshotPath = Path.Combine(FindRepoRoot(), "schema.graphql");

        if (Environment.GetEnvironmentVariable("UPDATE_SCHEMA") == "1" || !File.Exists(snapshotPath))
        {
            await File.WriteAllTextAsync(snapshotPath, actual);
        }

        var expected = Normalize(await File.ReadAllTextAsync(snapshotPath));
        Assert.True(expected == actual, "schema.graphql is out of date. Re-run tests with UPDATE_SCHEMA=1 and commit the diff.");
    }

    [Fact]
    public async Task Domain_methods_are_not_exposed_as_query_fields()
    {
        var client = await factory.CreateGraphQLClientAsync();
        var sdl = await client.Http.GetStringAsync("/graphql?sdl");

        Assert.DoesNotContain("moveTo(", sdl, StringComparison.Ordinal);
    }

    private static string Normalize(string sdl) => sdl.ReplaceLineEndings("\n").Trim() + "\n";

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HireFlow.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
