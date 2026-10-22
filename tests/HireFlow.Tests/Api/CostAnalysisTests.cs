using HireFlow.Api.Auth;

namespace HireFlow.Tests.Api;

/// <summary>
/// Real client queries (taken from the HireFlow web app) must fit the cost budget when they pass
/// filters as variables, while genuinely expensive queries are still rejected.
/// </summary>
public sealed class CostAnalysisTests(HireFlowApiFactory factory) : IClassFixture<HireFlowApiFactory>
{
    private const string JobBoardQuery = """
        query JobBoard($where: JobPostingFilterInput, $after: String) {
          jobs(where: $where, first: 12, after: $after, order: [{ publishedAt: DESC }]) {
            totalCount
            pageInfo { hasNextPage endCursor }
            nodes {
              id title location workMode employmentType status requiredSkills publishedAt
              salary { min max currency }
              company { id name }
              pipeline { total }
            }
          }
        }
        """;

    [Fact]
    public async Task Filtered_job_board_query_with_variables_is_accepted()
    {
        var client = await factory.CreateGraphQLClientAsync(Roles.Recruiter);

        var result = await client.ExecuteAsync(JobBoardQuery, new
        {
            where = new
            {
                and = new object[]
                {
                    new { title = new { contains = "Engineer" } },
                    new { workMode = new { @in = new[] { "REMOTE", "HYBRID" } } },
                    new { status = new { eq = "OPEN" } },
                },
            },
        });

        Assert.False(result.HasErrors(), result.ToString());
        Assert.True(result.At("data.jobs.totalCount").GetInt32() > 0);
    }

    [Fact]
    public async Task Filtered_candidate_search_with_variables_is_accepted()
    {
        var client = await factory.CreateGraphQLClientAsync(Roles.Recruiter);

        var result = await client.ExecuteAsync("""
            query Candidates($where: CandidateFilterInput) {
              candidates(where: $where, first: 20, order: [{ fullName: ASC }]) {
                totalCount
                nodes { id fullName headline yearsOfExperience skills applications { id stage } }
              }
            }
            """, new { where = new { skills = new { some = new { eq = "C#" } } } });

        Assert.False(result.HasErrors(), result.ToString());
    }

    [Fact]
    public async Task Deeply_nested_fan_out_is_still_rejected()
    {
        var client = await factory.CreateGraphQLClientAsync(Roles.Recruiter);

        var result = await client.ExecuteAsync("""
            {
              companies(first: 50) { nodes { jobs { applications { candidate { applications { job { applications { history { to } } } } } } } } }
            }
            """);

        Assert.Equal("HC0047", result.At("errors.0.extensions.code").GetString());
    }
}
