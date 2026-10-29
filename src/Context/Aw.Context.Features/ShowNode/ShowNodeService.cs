using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Records;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ShowNode;

/// <summary>One node, its aliases and its edges, from the map the caller may read.</summary>
internal sealed class ShowNodeService(MapReader reader, IScrubberFactory scrubbers, IRecordStore records, IRepoRemote remotes) : IService<ShowNodeRequest, NodeView>
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
        var url = await SourceUrl(request.Scope, map, details.TargetOf(node.Value), cancellationToken).ConfigureAwait(false);
        return new NodeView(map.Snapshot.Label, details.Node(node.Value, url),
            [.. graph.Aliases.Where(a => a.Target.Value == key).Select(a => a.Alias).OrderBy(a => a.Value, StringComparer.Ordinal)],
            [.. graph.Edges.Where(e => e.From.Value == key).OrderBy(e => e.To.Value, StringComparer.Ordinal).ThenBy(e => e.Type).Select(e => details.Edge(EdgeFact.Of(e), e.To))],
            [.. graph.Edges.Where(e => e.To.Value == key).OrderBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.Type).Select(e => details.Edge(EdgeFact.Of(e), e.From))]);
    }

    // The link of the node's source; any failure on the way (settings, scrubbers, the store, the remote) is no link, never a failed show.
    private async ValueTask<Uri?> SourceUrl(ContextScope scope, VisibleMap map, SourceTarget? target, CancellationToken cancellationToken)
    {
        if (target is null)
        {
            return null;
        }

        var settings = SettingsReader.Read(scope.Settings);
        var built = settings.Bind(s => scrubbers.For(s.ExtraPatterns));
        return settings.IsError || built.IsError
            ? null
            : await new SourceLinks(scope, map, built.Value.Egress, settings.Value.LinearTeams.ToHashSet(StringComparer.Ordinal), records, remotes).Of(target, cancellationToken).ConfigureAwait(false);
    }
}
