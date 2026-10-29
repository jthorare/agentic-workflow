using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;

namespace Aw.Context.Core.Map;

/// <summary>
/// What a lenses answer rests on besides the graph (scoping spec C6, C13): the version's moment and commit, the repo's tracked files at HEAD (never a denied path) for map
/// coverage, and the intent source's row. A repo that can't be read leaves the files empty, so coverage counts nothing rather than failing the lenses.
/// </summary>
public sealed class LensBasisReader(IRepoReader repos, IIntentStore intents, TimeProvider clock, IIndexSync? sync = null)
{
    private static readonly SourceId _onyx = SourceId.From("onyx");

    /// <summary>The basis of a lenses or code answer over one visible map.</summary>
    /// <param name="map">The map the caller may read.</param>
    /// <param name="settings">The checked settings.</param>
    /// <param name="scope">The profile's repos and the caller.</param>
    /// <param name="draft">True when the draft was read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The basis.</returns>
    public async ValueTask<LensBasis> For(VisibleMap map, ContextSettings settings, ContextScope scope, bool draft, CancellationToken cancellationToken)
    {
        var version = map.Snapshot.Version;
        var sources = await IntentSources(settings, scope, cancellationToken).ConfigureAwait(false);
        return new LensBasis(map.Snapshot.Tag, draft, clock.GetUtcNow())
        {
            MapCreated = version.Created,
            MapCommit = ShortCommit(version.Commit),
            MapRepo = version.Repo.Value,
            Files = await Files(version.Repo, settings, scope, cancellationToken).ConfigureAwait(false),
            IntentSources = sources,
        };
    }

    // The owned index's sources with their sync state, then the retired Onyx row while the profile still names it. A progress read that fails (or no index sync) adds no index rows.
    private async ValueTask<IReadOnlyList<SearchedSource>> IntentSources(ContextSettings settings, ContextScope scope, CancellationToken cancellationToken)
    {
        var rows = new List<SearchedSource>();
        var progress = sync is null ? null : await sync.Progress(scope, cancellationToken).ConfigureAwait(false);
        if (progress is { IsError: false })
        {
            rows.AddRange(progress.Value.Select(SearchedSources.Index));
        }

        if (settings.Onyx is not null)
        {
            var state = await intents.State(_onyx, cancellationToken).ConfigureAwait(false);
            rows.Add(SearchedSources.Intent(_onyx, configured: true, state.IsError ? null : state.Value));
        }

        return rows;
    }

    /// <summary>The commit as 'mapCommit' shows it: exactly 7 lowercase hex characters, or null when there is no commit or it isn't a sha of 7 or more hex characters.</summary>
    public static string? ShortCommit(string? commit) =>
        commit is { Length: >= 7 } && commit.All(char.IsAsciiHexDigit) ? commit[..7].ToLowerInvariant() : null;

    private async ValueTask<IReadOnlyList<string>> Files(ContextRepoName name, ContextSettings settings, ContextScope scope, CancellationToken cancellationToken)
    {
        var inScope = ScopeRules.Resolve(settings, scope.Repos, scope.Caller);
        if (inScope.IsError || inScope.Value.FirstOrDefault(r => r.Name == name) is not { } repo)
        {
            return [];
        }

        var head = await repos.Head(repo, cancellationToken).ConfigureAwait(false);
        var listed = head.IsError ? null : await repos.ListFiles(repo, head.Value, cancellationToken).ConfigureAwait(false);
        return listed is null || listed.IsError ? [] : [.. listed.Value.Where(p => !repo.Denies(p))];
    }
}
