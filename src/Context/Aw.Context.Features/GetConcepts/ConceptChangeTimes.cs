using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.History;

namespace Aw.Context.Features.GetConcepts;

/// <summary>
/// The newest change time of code files, per repo and keyed by the repo's HEAD sha (#3760). A read only looks in the cache: git takes seconds over a few hundred files, so a miss
/// answers with what it has and starts one fill per repo in the background. The fill asks git for the paths the cache has not asked about yet and merges the answer in, so the next
/// page needs git only for its own new paths. A new HEAD drops the repo's times and starts again.
/// </summary>
internal sealed class ConceptChangeTimes(IFileHistory history, IRepoReader repos)
{
    // Newest first, so a file's first appearance is its last change; files untouched within this many commits stay unknown until HEAD moves.
    internal const int ChangesPerRepo = 500;

    // How long a HEAD read stands for the repo's HEAD (#3837); a stub until the cache uses it.
    internal static readonly TimeSpan HeadFor = TimeSpan.FromSeconds(30);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, RepoTimes> _byRepo = new(StringComparer.Ordinal);

    /// <summary>The times the cache holds for <paramref name="paths"/>, newest across the repos; starts a background fill for the paths it has not asked about. Never waits on git's log.</summary>
    /// <param name="scopeRepos">The repos to look in.</param>
    /// <param name="paths">Repo-relative code paths.</param>
    /// <param name="cancellationToken">Cancels the HEAD read only, never a fill.</param>
    /// <returns>The known times by path.</returns>
    internal async ValueTask<IReadOnlyDictionary<string, DateTimeOffset>> Read(IEnumerable<ContextRepo> scopeRepos, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var known = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        if (paths.Count == 0)
        {
            return known;
        }

        foreach (var repo in scopeRepos)
        {
            var head = await repos.Head(repo, cancellationToken).ConfigureAwait(false);
            if (head.IsError)
            {
                continue;
            }

            lock (_gate)
            {
                var state = StateFor(repo, head.Value);
                foreach (var path in paths.Where(state.Times.ContainsKey))
                {
                    var at = state.Times[path];
                    if (!known.TryGetValue(path, out var held) || at > held)
                    {
                        known[path] = at;
                    }
                }

                var missing = paths.Where(p => !state.Asked.Contains(p)).ToList();
                if (missing.Count > 0 && state.Fill is null)
                {
                    state.Fill = Task.Run(() => Fill(repo, state, missing), CancellationToken.None);   // a fill outlives the read that started it
                }
            }
        }

        return known;
    }

    /// <summary>A task that ends when no fill is running; a test awaits it instead of timing.</summary>
    /// <returns>The task.</returns>
    internal Task WhenIdle()
    {
        lock (_gate)
        {
            return Task.WhenAll(_byRepo.Values.Select(s => s.Fill).OfType<Task>().ToList());
        }
    }

    // Callers hold the gate.
    private RepoTimes StateFor(ContextRepo repo, string head)
    {
        var key = repo.Name.Value + "\0" + repo.Path;
        if (!_byRepo.TryGetValue(key, out var state) || !string.Equals(state.Head, head, StringComparison.Ordinal))
        {
            _byRepo[key] = state = new RepoTimes(head);
        }

        return state;
    }

    // A failed read still counts as asked: a broken repo costs one git call per HEAD, not one per request.
    private async Task Fill(ContextRepo repo, RepoTimes state, List<string> missing)
    {
        try
        {
            var answer = await history.Changes(repo, new FileHistoryQuery(state.Head, missing, HistoryWindow.Open, ChangesPerRepo), CancellationToken.None).ConfigureAwait(false);
            lock (_gate)
            {
                if (!answer.IsError)
                {
                    foreach (var file in answer.Value.Changes.SelectMany(c => c.Files.Select(f => (File: f, c.At))))
                    {
                        _ = state.Times.TryAdd(file.File, file.At);
                    }
                }

                state.Asked.UnionWith(missing);
            }
        }
        finally
        {
            lock (_gate)
            {
                state.Fill = null;
            }
        }
    }

    private sealed class RepoTimes(string head)
    {
        internal string Head { get; } = head;

        internal Dictionary<string, DateTimeOffset> Times { get; } = new(StringComparer.Ordinal);

        internal HashSet<string> Asked { get; } = new(StringComparer.Ordinal);

        internal Task? Fill { get; set; }
    }
}
