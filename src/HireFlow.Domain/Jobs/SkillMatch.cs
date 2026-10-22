namespace HireFlow.Domain.Jobs;

/// <summary>How well a candidate's skills cover a posting's requirements.</summary>
public sealed record SkillMatch(double Score, IReadOnlyList<string> MatchedSkills, IReadOnlyList<string> MissingSkills)
{
    /// <summary>
    /// Case-insensitive overlap between required and offered skills. <see cref="Score"/> is the
    /// share of required skills the candidate has (0..1); a posting with no requirements matches fully.
    /// </summary>
    public static SkillMatch Evaluate(IEnumerable<string> requiredSkills, IEnumerable<string> candidateSkills)
    {
        var offered = candidateSkills.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var required = requiredSkills.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var matched = required.Where(offered.Contains).ToList();
        var missing = required.Where(s => !offered.Contains(s)).ToList();
        var score = required.Count == 0 ? 1.0 : Math.Round((double)matched.Count / required.Count, 3);

        return new SkillMatch(score, matched, missing);
    }
}
