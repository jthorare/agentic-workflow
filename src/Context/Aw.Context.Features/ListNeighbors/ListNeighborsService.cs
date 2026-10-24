using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ListNeighbors;

/// <summary>The nodes within some edges of one node, either way, and the edges among them (depth 0 to 4, default 1; spec §8.3).</summary>
internal sealed class ListNeighborsService(MapReader reader, IScrubberFactory scrubbers) : IService<ListNeighborsRequest, NeighborsView>
{
    public async ValueTask<Result<NeighborsView>> Handle(ListNeighborsRequest request, CancellationToken cancellationToken)
    {
        var asked = request.Depth ?? 1;
        if (QueryRules.CheckDepth(asked) is { } depthError)
        {
            return depthError;
        }

        var depth = Math.Min(asked, QueryRules.MaxDepth);

        var call = await reader.Open(request.Scope, scrubbers, cancellationToken).ConfigureAwait(false);
        var node = call.Bind(c => AnchorMatch.Resolve(c.Map.Graph, request.Key));
        if (node.IsError)
        {
            return node.Error;
        }

        var (map, text) = call.Value;
        var hood = GraphWalk.Neighbors(map.Graph, node.Value.Key, depth);
        return new NeighborsView(map.Snapshot.Label, hood.Center, depth, [.. hood.Nodes.Select(n => new NeighborFact(text.Fact(n.Node), n.Depth))],
            [.. hood.Edges.Select(EdgeFact.Of)]);
    }
}
