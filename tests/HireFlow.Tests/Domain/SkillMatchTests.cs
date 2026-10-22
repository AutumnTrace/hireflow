using HireFlow.Domain.Jobs;

namespace HireFlow.Tests.Domain;

public sealed class SkillMatchTests
{
    [Fact]
    public void Score_is_share_of_required_skills_covered()
    {
        var match = SkillMatch.Evaluate(["C#", "GraphQL", "PostgreSQL", "Docker"], ["c#", "graphql", "React"]);

        Assert.Equal(0.5, match.Score);
        Assert.Equal(["C#", "GraphQL"], match.MatchedSkills);
        Assert.Equal(["PostgreSQL", "Docker"], match.MissingSkills);
    }

    [Fact]
    public void Posting_without_requirements_is_a_full_match()
    {
        var match = SkillMatch.Evaluate([], ["C#"]);

        Assert.Equal(1.0, match.Score);
        Assert.Empty(match.MissingSkills);
    }

    [Fact]
    public void Candidate_without_skills_scores_zero()
    {
        var match = SkillMatch.Evaluate(["C#", "SQL"], []);

        Assert.Equal(0.0, match.Score);
        Assert.Equal(2, match.MissingSkills.Count);
    }

    [Fact]
    public void Score_is_rounded_to_three_decimals()
        => Assert.Equal(0.333, SkillMatch.Evaluate(["A", "B", "C"], ["a"]).Score);
}
