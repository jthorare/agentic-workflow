using Aw.Context.Core;
using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Symbols;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetFileMap;

/// <summary>
/// The direct children of a repo path with their status (spec §8.9): the repo's files at HEAD (never a denied path) against the
/// map the caller may read, built by FileMapBuilder. A repo outside the profile, outside 'context.repos' or outside a
/// session's scope is SND-CONTEXT-045 with one answer for every reason, so a scope never reveals what is outside it.
/// </summary>
internal sealed class GetFileMapService(MapReader reader, IRepoReader repos, ICodeSymbolSource symbols) : IService<GetFileMapRequest, FileMapView>
{
    public async ValueTask<Result<FileMapView>> Handle(GetFileMapRequest request, CancellationToken cancellationToken)
    {
        var query = request.Query;
        var repo = Addressed(request.Scope, query.Repo);
        var map = repo.IsError ? repo.Error : await reader.ReadView(request.Scope, query.Draft, cancellationToken).ConfigureAwait(false);
        var tree = map.IsError ? map.Error : await Tree(repo.Value, cancellationToken).ConfigureAwait(false);
        if (repo.IsError || map.IsError || tree.IsError)
        {
            return repo.IsError ? repo.Error : map.IsError ? map.Error : tree.Error;
        }

        var built = FileMapBuilder.Build(new FileMapBasis(map.Value.Snapshot.Tag, tree.Value.Sha), query, tree.Value.Files, map.Value.Graph);
        return built.IsError ? built.Error : built.Value with { Symbols = await Symbols(repo.Value, map.Value.Snapshot.Version, built.Value, cancellationToken).ConfigureAwait(false) };
    }

    // The symbols column (S-9a, links in Phase 9): what the code index says about the files this listing shows, as of the map version's commit. A directory row has none.
    private ValueTask<LensSymbols> Symbols(ContextRepo repo, MapVersion version, FileMapView view, CancellationToken cancellationToken)
    {
        var files = view.Entries.Where(e => e.Type == FileEntryType.File).ToList();
        var links = files.Select(e => (e.Path, (IReadOnlyList<NodeKey>)[.. e.Features.Where(f => f.Edge == EdgeType.Implements).Select(f => f.Key)])).Where(l => l.Item2.Count > 0).ToList();
        return SymbolColumn.Read(symbols, new SymbolColumnReading(repo, version, [.. files.Select(e => e.Path)], links), cancellationToken);
    }

    private static Result<ContextRepo> Addressed(ContextScope scope, ContextRepoName name)
    {
        var addressed = SettingsReader.Read(scope.Settings).Bind(s => ScopeRules.Resolve(s, scope.Repos, scope.Caller));
        if (addressed.IsError && addressed.Error.Code != ContextErrors.UnknownAnchor.Code)
        {
            return addressed.Error;
        }

        var repo = addressed.IsError ? null : addressed.Value.FirstOrDefault(r => r.Name == name);
        return repo is null
            ? ContextErrors.RepoOutsideScope.With("that repo isn't one this caller can read (fix: name a repo of the approved profile; a session names its own X-Sindri-Repo)")
            : repo;
    }

    /// <summary>The repo's tracked files at HEAD (never a denied path) and the first seven characters of that commit.</summary>
    private async ValueTask<Result<(string Sha, IReadOnlyList<string> Files)>> Tree(ContextRepo repo, CancellationToken cancellationToken)
    {
        var head = await repos.Head(repo, cancellationToken).ConfigureAwait(false);
        var listed = head.IsError ? head.Error : await repos.ListFiles(repo, head.Value, cancellationToken).ConfigureAwait(false);
        return listed.IsError ? listed.Error : (head.Value[..Math.Min(7, head.Value.Length)], [.. listed.Value.Where(p => !repo.Denies(p))]);
    }
}
