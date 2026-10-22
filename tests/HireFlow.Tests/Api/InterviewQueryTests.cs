using HireFlow.Api.Auth;

namespace HireFlow.Tests.Api;

public sealed class InterviewQueryTests(HireFlowApiFactory factory) : IClassFixture<HireFlowApiFactory>
{
    [Fact]
    public async Task Upcoming_interviews_only_include_the_future_window()
    {
        var client = await factory.CreateGraphQLClientAsync(Roles.Recruiter);

        // Seed data: one technical interview in 2 days, two interviews in the past.
        var result = await client.ExecuteAsync("""
            { upcomingInterviews(days: 7) { kind interviewer scheduledAt application { candidate { fullName } } } }
            """);

        Assert.False(result.HasErrors(), result.ToString());
        var interview = Assert.Single(result.At("data.upcomingInterviews").EnumerateArray());
        Assert.Equal("TECHNICAL", interview.At("kind").GetString());
        Assert.Equal("Ana Popescu", interview.At("application.candidate.fullName").GetString());
    }

    [Fact]
    public async Task Upcoming_interviews_can_be_filtered_by_interviewer()
    {
        var client = await factory.CreateGraphQLClientAsync(Roles.Recruiter);

        var result = await client.ExecuteAsync("""
            {
              mine: upcomingInterviews(interviewer: "Victor Moraru") { interviewer }
              other: upcomingInterviews(interviewer: "Somebody Else") { interviewer }
            }
            """);

        Assert.Equal(1, result.At("data.mine").GetArrayLength());
        Assert.Equal(0, result.At("data.other").GetArrayLength());
    }

    [Fact]
    public async Task Interviews_support_filtering_on_feedback()
    {
        var client = await factory.CreateGraphQLClientAsync(Roles.Recruiter);

        var result = await client.ExecuteAsync("""
            { interviews(where: { rating: { gte: 4 } }, order: { rating: DESC }) { totalCount nodes { rating } } }
            """);

        Assert.False(result.HasErrors(), result.ToString());
        Assert.Equal(2, result.At("data.interviews.totalCount").GetInt32());
        Assert.Equal(5, result.At("data.interviews.nodes.0.rating").GetInt32());
    }

    [Fact]
    public async Task Interview_agenda_requires_authentication()
    {
        var client = await factory.CreateGraphQLClientAsync();

        var result = await client.ExecuteAsync("{ upcomingInterviews { id } }");

        Assert.Equal("AUTH_NOT_AUTHENTICATED", result.At("errors.0.extensions.code").GetString());
    }
}
