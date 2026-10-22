using Aw.Context.Core.Ranking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Trust;

namespace Aw.Context.Features.QueryContext;

internal sealed partial class QueryPipeline
{
    private async Task<List<FusedItem>> Ranked(Searched searched, (GraphPart Graph, RankerPlan Plan) ranker, IEgressScrubber egress, List<DegradedSource> degraded, CancellationToken ct)
    {
        var ranked = await _ranker.Apply(RankFusion.Fuse(searched.Answers), ranker.Plan, ranker.Graph.RecordSignals, egress, ct).ConfigureAwait(false);
        if (ranked.Degraded is { } rerank)
        {
            degraded.Add(rerank);
        }

        return [.. ranked.Items.Take(MaxItems)];
    }
}
