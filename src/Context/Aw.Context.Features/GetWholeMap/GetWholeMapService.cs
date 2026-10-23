using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Trust;
using Aw.Context.Core.WholeMap;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetWholeMap;

/// <summary>
/// The whole map in one answer (Part D): the map the caller may read (a session's graph is already cut to its repo), its areas by <see cref="MapAreas"/>, the answer built by
/// <see cref="WholeMapBuilder"/> within the <see cref="WholeMapRules"/>, every label cleaned like every answer's, and the last few answers kept per graph, question and caller scope.
/// </summary>
internal sealed class GetWholeMapService(MapReader reader, IScrubberFactory scrubbers, WholeMapCache cache, WholeMapRules rules) : IService<GetWholeMapRequest, WholeMapView>
{
    public async ValueTask<Result<WholeMapView>> Handle(GetWholeMapRequest request, CancellationToken cancellationToken)
    {
        var (query, scope) = (request.Query, request.Scope);
        var detail = Detail(query);
        var settings = SettingsReader.Read(scope.Settings);
        var built = settings.Bind(s => scrubbers.For(s.ExtraPatterns));
        if (detail.IsError || built.IsError)
        {
            return detail.IsError ? detail.Error : built.Error;
        }

        var repo = Addressed(settings.Value, scope, query.Repo);
        if (repo.IsError)
        {
            return repo.Error;
        }

        var read = await reader.ReadViewAt(scope, query.Read, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var map = read.Value;
        var key = CacheKey(map.Snapshot.Version.Number, detail.Value, query, repo.Value, scope);
        return cache.Get(map.Graph, key, () => Build(map, (query, detail.Value), repo.Value, new MapText(built.Value, FencePolicies.For(scope.Caller))));
    }

    private Result<WholeMapView> Build(VisibleMap map, (WholeMapQuery Query, WholeMapDetail Detail) asked, RepoChoice repo, MapText text)
    {
        var graph = repo.Cut is { } cut ? map.Graph.ScopedTo(cut) : map.Graph;
        var snapshot = map.Snapshot;
        var commit = snapshot.Version.Commit is { Length: > 0 } c ? c[..Math.Min(7, c.Length)] : null;
        var basis = new WholeMapBasis(snapshot.Tag, commit, asked.Query.Read.Draft, snapshot.Version.Created, repo.Name);
        return WholeMapBuilder.Build(new WholeMapAsk(basis, asked.Detail, asked.Query.Locate, asked.Query.Area), graph, MapAreas.Assign(graph, rules.AreaCap), rules, text);
    }

    private static string CacheKey(int version, WholeMapDetail detail, WholeMapQuery query, RepoChoice repo, ContextScope scope) =>
        string.Join('\n', version, query.Read.Draft, detail, string.Join('\u001f', query.Locate), query.Area, repo.Name, repo.Cut?.Value, FencePolicies.For(scope.Caller), scope.Caller.Kind, scope.Caller.Repo?.Value, scope.Settings?.GetRawText());

    private static Result<WholeMapDetail> Detail(WholeMapQuery query)
    {
        if (query.Locate.Count > WholeMapRules.MaxLocate)
        {
            return ContextErrors.QueryInvalid.With($"'locate' names {query.Locate.Count} keys; the limit is {WholeMapRules.MaxLocate} (fix: ask for fewer keys, or ask again for the rest)");
        }

        return query.Detail switch
        {
            null or "" or "auto" => WholeMapDetail.Auto,
            "nodes" => WholeMapDetail.Nodes,
            "areas" => WholeMapDetail.Areas,
            _ => ContextErrors.QueryInvalid.With("'detail' is auto, nodes or areas (fix: use one of the three)"),
        };
    }

    // Who the answer is for: a session its own repo, a human the repo they named (a profile with several cuts the graph to it) or the profile's only repo.
    private static Result<RepoChoice> Addressed(ContextSettings settings, ContextScope scope, string? asked)
    {
        var resolved = ScopeRules.Resolve(settings, scope.Repos, scope.Caller);
        if (resolved.IsError && resolved.Error.Code != ContextErrors.UnknownAnchor.Code)
        {
            return resolved.Error;
        }

        var repos = resolved.IsError ? [] : resolved.Value;
        if (asked is null)
        {
            return new RepoChoice(repos.Count == 1 ? repos[0].Name.Value : scope.Caller.Repo?.Value, Cut: null);
        }

        var named = repos.FirstOrDefault(r => string.Equals(r.Name.Value, asked, StringComparison.Ordinal));
        return named is null
            ? ContextErrors.RepoOutsideScope.With("that repo isn't one this caller can read (fix: name a repo of the approved profile; a session names its own X-Sindri-Repo)")
            : new RepoChoice(named.Name.Value, repos.Count > 1 && scope.Caller.Kind != ContextCallerKind.Session ? named.Name : null);
    }

    private readonly record struct RepoChoice(string? Name, ContextRepoName? Cut);
}
