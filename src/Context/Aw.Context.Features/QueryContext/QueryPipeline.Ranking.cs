using Aw.Context.Core.Ranking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Trust;

namespace Aw.Context.Features.QueryContext;

internal sealed partial class QueryPipeline
{
    private async Task<List<FusedItem>> Ranked(Searched searched, (GraphPart Graph, RankerPlan Plan) ranker, IEgressScrubber egress, List<DegradedSource> degraded, CancellationToken ct)
    {
        var ranked = await _ranker.Apply(Fused(searched.Answers, ranker), ranker.Plan, ranker.Graph.RecordSignals, egress, ct).ConfigureAwait(false);
        if (ranked.Degraded is { } rerank)
        {
            degraded.Add(rerank);
        }

        return [.. ranked.Items];
    }

    // The pin leg (concept pins spec §3.3): plain fusion first, then the items inside the anchor's pin as one more leg. It needs exactly one anchor pin and no
    // concept= (every result is already in that pin); otherwise the plain fusion stands. It reads PinContext, so no step was added to Run.
    private static IReadOnlyList<FusedItem> Fused(IReadOnlyList<SourceAnswer> answers, (GraphPart Graph, RankerPlan Plan) ranker)
    {
        var plain = RankFusion.Fuse(answers);
        var pins = ranker.Graph.Pins;
        if (!ranker.Plan.PinLeg || pins.Scope is not null || pins.SoleAnchorPin is not { } anchorPin)
        {
            return plain;
        }

        var scope = pins.Pins.Scope(anchorPin.Value, ranker.Graph.MapVersion ?? string.Empty);
        if (scope.IsError)
        {
            return plain;
        }

        var leg = PinLeg.Of(plain, scope.Value);
        return leg.Keys.Count == 0 ? plain : RankFusion.Fuse(answers, [leg]);
    }
}
