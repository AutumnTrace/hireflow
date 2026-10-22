using System.Text.Json;
using HireFlow.Api.Auth;

namespace HireFlow.Tests.Api;

public sealed class GraphQLApiTests(HireFlowApiFactory factory) : IClassFixture<HireFlowApiFactory>
{
    [Fact]
    public async Task Anonymous_callers_only_see_open_jobs()
    {
        var client = await factory.CreateGraphQLClientAsync();

        var result = await client.ExecuteAsync("{ jobs(first: 50) { nodes { title status company { name } } } }");

        Assert.False(result.HasErrors(), result.ToString());
        var jobs = result.At("data.jobs.nodes").EnumerateArray().ToList();
        Assert.NotEmpty(jobs);
        Assert.All(jobs, j => Assert.Equal("OPEN", j.At("status").GetString()));
        Assert.All(jobs, j => Assert.False(string.IsNullOrEmpty(j.At("company.name").GetString())));
    }

    [Fact]
    public async Task Jobs_support_filtering_and_sorting()
    {
        var client = await factory.CreateGraphQLClientAsync();

        var result = await client.ExecuteAsync("""
            {
              jobs(where: { workMode: { eq: REMOTE } }, order: { title: ASC }) {
                totalCount
                nodes { title workMode }
              }
            }
            """);

        var titles = result.At("data.jobs.nodes").EnumerateArray().Select(j => j.At("title").GetString()).ToList();
        Assert.Equal(titles.Order(StringComparer.Ordinal), titles);
        Assert.All(result.At("data.jobs.nodes").EnumerateArray(), j => Assert.Equal("REMOTE", j.At("workMode").GetString()));
        Assert.Equal(titles.Count, result.At("data.jobs.totalCount").GetInt32());
    }

    [Fact]
    public async Task Candidate_data_requires_authentication()
    {
        var client = await factory.CreateGraphQLClientAsync();

        var result = await client.ExecuteAsync("{ candidates { nodes { email } } }");

        Assert.Equal("AUTH_NOT_AUTHENTICATED", result.At("errors.0.extensions.code").GetString());
    }

    [Fact]
    public async Task Mutations_require_the_recruiter_role()
    {
        var client = await factory.CreateGraphQLClientAsync();

        var result = await client.ExecuteAsync("""mutation { createCompany(input: { name: "Nope Inc" }) { company { id } } }""");

        Assert.True(result.HasErrors());
        Assert.StartsWith("AUTH_", result.At("errors.0.extensions.code").GetString());
    }

    [Fact]
    public async Task Recruiter_can_run_the_full_hiring_flow()
    {
        var client = await factory.CreateGraphQLClientAsync(Roles.Recruiter);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var companyId = await MutateAsync(client, "createCompany.company.id", $$"""
            mutation { createCompany(input: { name: "Acme {{suffix}}", industry: "Testing" }) {
              company { id } errors { __typename } } }
            """);

        var jobId = await MutateAsync(client, "createJobPosting.jobPosting.id", $$"""
            mutation { createJobPosting(input: {
                companyId: "{{companyId}}", title: "QA Engineer", description: "Break things",
                workMode: HYBRID, employmentType: FULL_TIME,
                salary: { min: 30000, max: 40000, currency: "eur" }, requiredSkills: ["xUnit"] }) {
              jobPosting { id } errors { __typename } } }
            """);

        await MutateAsync(client, "publishJobPosting.jobPosting.id", $$"""
            mutation { publishJobPosting(input: { id: "{{jobId}}" }) { jobPosting { id } errors { __typename } } }
            """);

        var candidateId = await MutateAsync(client, "registerCandidate.candidate.id", $$"""
            mutation { registerCandidate(input: { fullName: "Jane Doe", email: "jane.{{suffix}}@example.com", yearsOfExperience: 3 }) {
              candidate { id } errors { __typename } } }
            """);

        var applicationId = await MutateAsync(client, "submitApplication.jobApplication.id", $$"""
            mutation { submitApplication(input: { jobPostingId: "{{jobId}}", candidateId: "{{candidateId}}" }) {
              jobApplication { id } errors { __typename } } }
            """);

        foreach (var stage in new[] { "SCREENING", "INTERVIEW", "OFFER", "HIRED" })
        {
            await MutateAsync(client, "moveApplication.jobApplication.id", $$"""
                mutation { moveApplication(input: { applicationId: "{{applicationId}}", stage: {{stage}} }) {
                  jobApplication { id } errors { __typename } } }
                """);
        }

        var result = await client.ExecuteAsync($$"""
            { job(id: "{{jobId}}") {
                salary { currency }
                pipeline { total hireRate }
                applications { stage allowedNextStages history { to } } } }
            """);

        Assert.False(result.HasErrors(), result.ToString());
        Assert.Equal("EUR", result.At("data.job.salary.currency").GetString());
        Assert.Equal(1, result.At("data.job.pipeline.total").GetInt32());
        Assert.Equal(1.0, result.At("data.job.pipeline.hireRate").GetDouble());
        var application = result.At("data.job.applications.0");
        Assert.Equal("HIRED", application.At("stage").GetString());
        Assert.Equal(0, application.At("allowedNextStages").GetArrayLength());
        Assert.Equal(5, application.At("history").GetArrayLength());
    }

    [Fact]
    public async Task Illegal_stage_jump_returns_a_typed_error()
    {
        var client = await factory.CreateGraphQLClientAsync(Roles.Recruiter);
        var applicationId = (await client.ExecuteAsync(
                "{ applications(where: { stage: { eq: APPLIED } }, first: 1) { nodes { id } } }"))
            .At("data.applications.nodes.0.id").GetString();

        var result = await client.ExecuteAsync($$"""
            mutation { moveApplication(input: { applicationId: "{{applicationId}}", stage: HIRED }) {
              jobApplication { id }
              errors { __typename ... on InvalidStageTransitionError { from to allowedStages } } } }
            """);

        Assert.False(result.HasErrors(), result.ToString());
        Assert.Equal(JsonValueKind.Null, result.At("data.moveApplication.jobApplication").ValueKind);
        var error = result.At("data.moveApplication.errors.0");
        Assert.Equal("InvalidStageTransitionError", error.At("__typename").GetString());
        Assert.Equal("APPLIED", error.At("from").GetString());
        Assert.Contains("SCREENING", error.At("allowedStages").EnumerateArray().Select(s => s.GetString()));
    }

    [Fact]
    public async Task Duplicate_applications_are_rejected()
    {
        var client = await factory.CreateGraphQLClientAsync(Roles.Recruiter);
        var existing = (await client.ExecuteAsync(
                "{ applications(first: 1) { nodes { job { id } candidate { id } } } }"))
            .At("data.applications.nodes.0");

        var result = await client.ExecuteAsync($$"""
            mutation { submitApplication(input: {
                jobPostingId: "{{existing.At("job.id")}}", candidateId: "{{existing.At("candidate.id")}}" }) {
              errors { __typename } } }
            """);

        Assert.Contains(
            result.At("data.submitApplication.errors").EnumerateArray().Select(e => e.At("__typename").GetString()),
            t => t is "DuplicateApplicationError" or "JobStatusError");
    }

    [Fact]
    public async Task Invalid_input_returns_field_level_validation_error()
    {
        var client = await factory.CreateGraphQLClientAsync(Roles.Recruiter);

        var result = await client.ExecuteAsync("""
            mutation { registerCandidate(input: { fullName: "X", email: "nope", yearsOfExperience: 1 }) {
              candidate { id } errors { __typename ... on DomainValidationError { field message } } } }
            """);

        var error = result.At("data.registerCandidate.errors.0");
        Assert.Equal("DomainValidationError", error.At("__typename").GetString());
        Assert.Equal("email", error.At("field").GetString());
    }

    [Fact]
    public async Task Candidates_get_ranked_job_recommendations_and_match_scores()
    {
        var client = await factory.CreateGraphQLClientAsync(Roles.Recruiter);

        var result = await client.ExecuteAsync("""
            {
              candidates(where: { email: { eq: "ana.popescu@example.com" } }) {
                nodes {
                  applications { job { title } skillMatch { score matchedSkills missingSkills } }
                  recommendedJobs(limit: 3) { job { title status } match { score } }
                }
              }
            }
            """);

        Assert.False(result.HasErrors(), result.ToString());
        var ana = result.At("data.candidates.nodes.0");

        var applied = ana.At("applications.0");
        Assert.Equal("Senior .NET Engineer", applied.At("job.title").GetString());
        Assert.Equal(0.5, applied.At("skillMatch.score").GetDouble());
        Assert.Equal(["GraphQL", "PostgreSQL"], applied.At("skillMatch.missingSkills").EnumerateArray().Select(s => s.GetString()));

        var recommendations = ana.At("recommendedJobs").EnumerateArray().ToList();
        Assert.NotEmpty(recommendations);
        Assert.All(recommendations, r => Assert.Equal("OPEN", r.At("job.status").GetString()));
        Assert.DoesNotContain(recommendations, r => r.At("job.title").GetString() == "Senior .NET Engineer");
        var scores = recommendations.Select(r => r.At("match.score").GetDouble()).ToList();
        Assert.Equal(scores.OrderDescending(), scores);
    }

    private static async Task<string> MutateAsync(GraphQLClient client, string idPath, string mutation)
    {
        var result = await client.ExecuteAsync(mutation);
        Assert.False(result.HasErrors(), result.ToString());

        var payload = result.At("data." + idPath.Split('.')[0]);
        if (payload.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
        {
            Assert.Fail($"Mutation returned domain errors: {errors}");
        }

        return result.At("data." + idPath).GetString()!;
    }
}
