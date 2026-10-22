using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

/// <summary>The backtest's way into the planner: <see cref="QueryPipeline"/> over the arm's map (or the live one) and its own sources.</summary>
internal sealed class ArmQuery(QueryPipeline pipeline, MapReader live) : IArmQuery
{
    public ValueTask<Result<ContextResult>> Ask(ArmQuestion question, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);
        IMapProvider map = question.Map is { } fixedMap ? new FixedMap(fixedMap) : new LiveMap(live);
        return pipeline.Run(new QueryContextRequest(question.Query, question.Scope), new PipelineInputs(map, new FixedSources(question.Sources), question.AsOf, Ranker: question.Ranker), cancellationToken);
    }
}
