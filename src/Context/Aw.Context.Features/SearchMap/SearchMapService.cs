using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Search;
using Aw.Context.Core.Trust;
using Aw.Context.Core.WholeMap;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.SearchMap;

/// <summary>
/// Ranked full-text search over the map the caller may read (a session's graph is already cut to its repo, and a deny glob hides its nodes): the question cut into words (006 for none), the graph
/// cut into documents by <see cref="MapSearchDocs"/>, one index per graph kept by <see cref="MapSearchCache"/>, and each hit written with its label, matched field and snippet cleaned like every answer's
/// text and fenced for the callers who get the fence when the node is untrusted.
/// </summary>
internal sealed class SearchMapService(MapReader reader, IScrubberFactory scrubbers, IMapSearchIndexFactory indexes, MapSearchCache cache, WholeMapRules rules) : IService<SearchMapRequest, MapSearchView>
{
    public async ValueTask<Result<MapSearchView>> Handle(SearchMapRequest request, CancellationToken cancellationToken)
    {
        var (query, scope) = (request.Query, request.Scope);
        var terms = MapSearchRules.Terms(query.Q);
        var limit = MapSearchRules.CheckLimit(query.Limit);
        var settings = SettingsReader.Read(scope.Settings);
        var built = settings.Bind(s => scrubbers.For(s.ExtraPatterns));
        if (terms.IsError || limit.IsError || built.IsError)
        {
            return terms.IsError ? terms.Error : limit.IsError ? limit.Error : built.Error;
        }

        var repo = RepoAddressing.Addressed(settings.Value, scope, query.Repo);
        if (repo.IsError)
        {
            return repo.Error;
        }

        var read = await reader.ReadViewAtOrEmpty(scope, query.Read, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var map = read.Value;
        var text = new MapText(built.Value, FencePolicies.For(scope.Caller));
        var indexed = cache.Get(map.Graph, CacheKey(map.Snapshot.Version.Number, query.Read.Draft, repo.Value, scope), () => Index(map, repo.Value, text));
        if (indexed.IsError)
        {
            return indexed.Error;
        }

        var page = indexed.Value.Index.Search(terms.Value, query.Area, limit.Value);
        return page.IsError ? page.Error : new MapSearchView(map.Snapshot.Tag, page.Value.Total, [.. page.Value.Matches.Select(m => Hit(indexed.Value.Nodes[m.Doc], m, (terms.Value, repo.Value.Name), text))]);
    }

    private Result<IndexedMap> Index(VisibleMap map, RepoChoice repo, MapText text)
    {
        var graph = repo.Cut is { } cut ? map.Graph.ScopedTo(cut) : map.Graph;
        var nodes = MapSearchDocs.Of(graph, MapAreas.Assign(graph, rules.AreaCap), text);
        var index = indexes.Build([.. nodes.Select(static n => n.Doc)]);
        return index.IsError ? index.Error : new IndexedMap(index.Value, nodes);
    }

    private static MapSearchHit Hit(IndexedNode indexed, MapSearchMatch match, (IReadOnlyList<string> Terms, string? Repo) asked, MapText text)
    {
        var (matched, snippet) = MapSearchSnippets.Of(indexed.Doc, asked.Terms);
        var node = indexed.Node;
        return new MapSearchHit(node.Key, node.Key.Kind, text.Label(node.Label, indexed.Trust), indexed.Repo ?? asked.Repo, indexed.Doc.Area, Math.Round(match.Score, 4, MidpointRounding.ToEven), matched, text.Body(snippet, indexed.Trust), indexed.Trust);
    }

    private static string CacheKey(int version, bool draft, RepoChoice repo, ContextScope scope) =>
        string.Join('\n', version, draft, repo.Name, repo.Cut?.Value, scope.Caller.Kind, scope.Caller.Repo?.Value, scope.Settings?.GetRawText());
}
