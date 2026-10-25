using HireFlow.Api.Auth;

namespace HireFlow.Tests.Api;

/// <summary>Runs on SQLite and, in CI, PostgreSQL: both used to be case-sensitive for <c>contains</c>.</summary>
public sealed class CaseInsensitiveFilterTests(HireFlowApiFactory factory) : IClassFixture<HireFlowApiFactory>
{
    [Fact]
    public async Task Built_in_contains_stays_case_sensitive()
    {
        var client = await factory.CreateGraphQLClientAsync(Roles.Recruiter);

        var result = await client.ExecuteAsync("""{ candidates(where: { fullName: { contains: "ana" } }) { nodes { fullName } } }""");

        Assert.False(result.HasErrors(), result.ToString());
        Assert.Empty(result.At("data.candidates.nodes").EnumerateArray());
    }

    [Fact]
    public async Task Icontains_ignores_case()
    {
        var client = await factory.CreateGraphQLClientAsync(Roles.Recruiter);

        var result = await client.ExecuteAsync("""{ candidates(where: { fullName: { icontains: "pOPESCU" } }) { nodes { fullName } } }""");

        Assert.False(result.HasErrors(), result.ToString());
        var names = result.At("data.candidates.nodes").EnumerateArray().Select(n => n.At("fullName").GetString()).ToList();
        Assert.Equal(["Ana Popescu"], names);
    }

    [Fact]
    public async Task Istarts_with_ignores_case_and_works_with_variables()
    {
        var client = await factory.CreateGraphQLClientAsync();

        var result = await client.ExecuteAsync(
            """
            query Jobs($where: JobPostingFilterInput) {
              jobs(where: $where, order: { title: ASC }) { nodes { title } }
            }
            """,
            new { where = new { title = new { istartsWith = "sENIOR" } } });

        Assert.False(result.HasErrors(), result.ToString());
        var titles = result.At("data.jobs.nodes").EnumerateArray().Select(n => n.At("title").GetString()).ToList();
        Assert.Equal(["Senior .NET Engineer"], titles);
    }

    [Fact]
    public async Task Icontains_combines_with_other_filters()
    {
        var client = await factory.CreateGraphQLClientAsync(Roles.Recruiter);

        var result = await client.ExecuteAsync("""
            {
              candidates(where: { or: [{ fullName: { icontains: "ION" } }, { email: { icontains: "ELENA" } }] }, order: { fullName: ASC }) {
                nodes { fullName }
              }
            }
            """);

        Assert.False(result.HasErrors(), result.ToString());
        var names = result.At("data.candidates.nodes").EnumerateArray().Select(n => n.At("fullName").GetString()).ToList();
        Assert.Equal(["Elena Ciobanu", "Ion Rusu"], names);
    }
}
