using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

internal sealed partial class QueryPipeline
{
    // The pins of the query go on the graph part, where the legs, the graph and the ranker read them. The map is read again here only when a pin is in play
    // (concept= set, or a mapped repo has pins), so a query on a box with no pins pays nothing.
    private static async ValueTask<Result<(GraphPart Graph, IReadOnlyList<string> Terms, ItemMiss? Miss)>> Pinned(Result<(GraphPart Graph, IReadOnlyList<string> Terms, ItemMiss? Miss)> anchored,
        (QueryContextRequest Request, IReadOnlyList<ContextRepo> Visible, ContextSettings Settings) asked, (PipelineInputs Inputs, MapRead Read) source, CancellationToken cancellationToken)
    {
        if (anchored.IsError)
        {
            return anchored.Error;
        }

        var (request, visible, settings) = asked;
        var mapped = ScopeRules.Mapped(settings, request.Scope.Repos);
        var concept = source.Inputs.Concept;
        if (concept is null && !mapped.Any(r => r.Concepts.Count > 0))
        {
            return anchored;
        }

        var map = await source.Inputs.Map.Read(request.Scope, source.Read, cancellationToken).ConfigureAwait(false);
        var pins = PinContext.Of(mapped, visible, map.IsError ? null : map.Value, concept, anchored.Value.Graph.Anchors);
        if (pins.IsError)
        {
            return pins.Error;
        }

        var part = anchored.Value.Graph with { Pins = pins.Value };
        return (pins.Value.Scope is { } scope && !map.IsError ? PinCut.Apply(part, scope, map.Value.Graph) : part, anchored.Value.Terms, anchored.Value.Miss);
    }
}
