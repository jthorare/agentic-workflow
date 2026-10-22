using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Ranking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Features.RunBacktest;

/// <summary>
/// Runs one arm: the real planner (through <see cref="IArmQuery"/>) over the arm's map and sources. The brief is the question; the arms differ in the graph they
/// read and the sources they may search (spec §9.3).
/// </summary>
internal sealed class ArmRunner(IArmQuery planner, TimeProvider clock)
{
    /// <summary>Asks the arm's question.</summary>
    /// <param name="arm">The arm.</param>
    /// <param name="inputs">The sample's inputs.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The packed result; an arm whose query failed gives an empty result (it found nothing), never an error.</returns>
    public async ValueTask<ContextResult> Run(Arm arm, ArmInputs inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var empty = inputs.AsOfMap with { Graph = GraphSnapshot.Empty, Snapshot = inputs.AsOfMap.Snapshot with { Graph = GraphSnapshot.Empty } };
        IReadOnlyList<IContextSource> docs = [inputs.Docs];
        (VisibleMap? Map, IReadOnlyList<IContextSource> Sources, int? Depth) setup = arm switch
        {
            Arm.Brief => (inputs.AsOfMap, [], 0),   // the text-anchor step alone: no source, no cascade
            Arm.Docs => (empty, docs, null),
            Arm.Graph => (inputs.AsOfMap, [], null),
            Arm.GraphReviewed => (null, [], null),   // the ceiling: today's map with every review decision
            Arm.All or Arm.RankerRrf or Arm.RankerFeatures or Arm.RankerRerank => (inputs.AsOfMap, docs, null),   // the ranker arms read what 'all' reads and differ only in the ranker stages
            Arm.Linear or Arm.Onyx or Arm.Code => throw new ArgumentOutOfRangeException(nameof(arm), "that arm is not available yet"),
            _ => throw new ArgumentOutOfRangeException(nameof(arm)),
        };
        var (map, sources, depth) = setup;
        var query = new ContextQuery(inputs.BriefText, Depth: depth);
        var answer = await planner.Ask(new ArmQuestion(inputs.Scope, query, map, sources, inputs.Sample.CreatedAt, Ranker(arm)), cancellationToken).ConfigureAwait(false);
        return answer.IsSuccess ? answer.Value : Nothing(query);
    }

    // The other arms run the profile's ranker (off unless a person turned it on).
    private static RankerMode? Ranker(Arm arm) => arm == Arm.RankerRrf ? RankerMode.Rrf : arm == Arm.RankerFeatures ? RankerMode.Features : arm == Arm.RankerRerank ? RankerMode.Rerank : null;

    private ContextResult Nothing(ContextQuery query) =>
        new(ContextResult.CurrentSchemaVersion, QueryId.From("q_backtest"), query, [], MapVersion: null, clock.GetUtcNow(), [], [], [], [], Truncated: false,
            new BudgetUse(0, 0, Estimate: true), new Dictionary<string, Citation>(StringComparer.Ordinal));
}
