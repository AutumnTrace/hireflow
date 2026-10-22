using HireFlow.Domain.Applications;

namespace HireFlow.Api.GraphQL;

/// <summary>Funnel snapshot for one job posting.</summary>
public sealed record PipelineStats(int Total, IReadOnlyList<StageCount> ByStage, double HireRate)
{
    public static PipelineStats From(IReadOnlyDictionary<ApplicationStage, int> counts)
    {
        var byStage = Enum.GetValues<ApplicationStage>()
            .Select(stage => new StageCount(stage, counts.GetValueOrDefault(stage)))
            .ToList();
        var total = byStage.Sum(s => s.Count);
        var hired = counts.GetValueOrDefault(ApplicationStage.Hired);
        return new PipelineStats(total, byStage, total == 0 ? 0 : Math.Round((double)hired / total, 3));
    }
}

public sealed record StageCount(ApplicationStage Stage, int Count);
