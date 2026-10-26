using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.FindPath;

/// <summary>The shortest path of at most 8 edges between two nodes, either way (spec §8.3); none is an answer, not an error.</summary>
internal sealed class FindPathService(MapReader reader, IScrubberFactory scrubbers) : IService<FindPathRequest, PathView>
{
    public async ValueTask<Result<PathView>> Handle(FindPathRequest request, CancellationToken cancellationToken)
    {
        var call = await reader.Open(request.Scope, scrubbers, cancellationToken).ConfigureAwait(false);
        var from = call.Bind(c => AnchorMatch.Resolve(c.Map.Graph, request.From, request.Scope.Repos));
        var to = from.Bind(_ => AnchorMatch.Resolve(call.Value.Map.Graph, request.To, request.Scope.Repos));
        if (to.IsError)
        {
            return to.Error;
        }

        var (map, text) = call.Value;
        var nodes = map.Graph.Nodes.ToDictionary(n => n.Key.Value, StringComparer.Ordinal);
        var repoOf = NodeRepos.Index(map.Graph, map.Snapshot.Version.Repo.Value);
        var path = GraphWalk.ShortestPath(map.Graph, from.Value.Key, to.Value.Key);
        return new PathView(map.Snapshot.Label, from.Value.Key, to.Value.Key, path is not null,
            [.. (path?.Steps ?? []).Select(s => new PathFact(EdgeFact.Of(s.Edge), s.Forward, text.Fact(nodes[s.Node.Value]) with { Repo = repoOf.GetValueOrDefault(s.Node.Value) }))]);
    }
}
