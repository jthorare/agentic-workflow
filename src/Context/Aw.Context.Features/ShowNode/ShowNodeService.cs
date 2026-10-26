using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ShowNode;

/// <summary>One node, its aliases and its edges, from the map the caller may read.</summary>
internal sealed class ShowNodeService(MapReader reader, IScrubberFactory scrubbers) : IService<ShowNodeRequest, NodeView>
{
    public async ValueTask<Result<NodeView>> Handle(ShowNodeRequest request, CancellationToken cancellationToken)
    {
        var call = await reader.Open(request.Scope, scrubbers, cancellationToken).ConfigureAwait(false);
        var node = call.Bind(c => AnchorMatch.Resolve(c.Map.Graph, request.Key, request.Scope.Repos));
        if (node.IsError)
        {
            return node.Error;
        }

        var (map, text) = call.Value;
        var key = node.Value.Key.Value;
        var graph = map.Graph;
        var details = new NodeDetails(graph, text, map.Snapshot.Version.Repo.Value);
        return new NodeView(map.Snapshot.Label, details.Node(node.Value),
            [.. graph.Aliases.Where(a => a.Target.Value == key).Select(a => a.Alias).OrderBy(a => a.Value, StringComparer.Ordinal)],
            [.. graph.Edges.Where(e => e.From.Value == key).OrderBy(e => e.To.Value, StringComparer.Ordinal).ThenBy(e => e.Type).Select(e => details.Edge(EdgeFact.Of(e), e.To))],
            [.. graph.Edges.Where(e => e.To.Value == key).OrderBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.Type).Select(e => details.Edge(EdgeFact.Of(e), e.From))]);
    }
}
