using Aw.Context.Core.Engine;

namespace Aw.Context.Features.BuildMap;

/// <summary>The build's path lists, to resolve a path: exactly, else by the one file whose name or whole trailing path segments match. Never fuzzy.</summary>
internal sealed class RepoPaths
{
    private readonly Dictionary<string, HashSet<string>> _byRepo;
    private readonly Dictionary<string, HashSet<string>> _removed;
    private readonly Dictionary<string, Dictionary<string, List<string>>> _byBaseName;
    private readonly List<string> _longestFirst;

    internal RepoPaths(IReadOnlyList<MapRepoFiles> repos)
    {
        _byRepo = repos.ToDictionary(r => r.Name.Value, r => r.Paths.ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        _removed = repos.ToDictionary(r => r.Name.Value, r => r.Removed.ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        _byBaseName = repos.ToDictionary(r => r.Name.Value, r => r.Paths.GroupBy(BaseName, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal), StringComparer.Ordinal);
        _longestFirst = [.. _byRepo.Keys.OrderByDescending(n => n.Length).ThenBy(n => n, StringComparer.Ordinal)];
    }

    /// <summary>Resolves a cited path: the exact path at HEAD (the record's own repo first, then the one other repo that has it), else an exact path some repo deleted (history), else the one file whose name or trailing path segments match.</summary>
    /// <param name="repo">The citing record's repo, or null for a record that names none.</param>
    /// <param name="path">The path.</param>
    /// <returns>How it fared and, when resolved, the repo that holds the file and the file's path.</returns>
    internal (CitationOutcome Outcome, string? Repo, string? Path) Resolve(ContextRepoName? repo, string path)
    {
        var holders = _byRepo.Where(r => r.Value.Contains(path)).Select(r => r.Key).ToList();
        if (holders.Count > 0)
        {
            if (repo is { } own && holders.Contains(own.Value, StringComparer.Ordinal))
            {
                return (CitationOutcome.Resolved, own.Value, path);
            }

            return holders.Count == 1 ? (CitationOutcome.Resolved, holders[0], path) : (CitationOutcome.Ambiguous, null, null);
        }

        if (_removed.Values.Any(r => r.Contains(path)))
        {
            return (CitationOutcome.History, null, null);
        }

        if (repo is { } mine && Candidates(mine.Value, path) is { Count: > 0 } ownFound)
        {
            return ownFound.Count == 1 ? (CitationOutcome.Resolved, mine.Value, ownFound[0]) : (CitationOutcome.Ambiguous, null, null);
        }

        var all = _byRepo.Keys.SelectMany(r => Candidates(r, path).Select(p => (Repo: r, Path: p))).Take(2).ToList();
        return all.Count switch
        {
            0 => (CitationOutcome.Missing, null, null),
            1 => (CitationOutcome.Resolved, all[0].Repo, all[0].Path),
            _ => (CitationOutcome.Ambiguous, null, null),
        };
    }

    // Files of the repo named like the cited path's last segment, and, when the citation has directories, ending with all of them on whole segments.
    private List<string> Candidates(string repo, string cited)
    {
        if (!_byBaseName.TryGetValue(repo, out var names) || !names.TryGetValue(BaseName(cited), out var named))
        {
            return [];
        }

        return cited.Contains('/', StringComparison.Ordinal)
            ? [.. named.Where(p => string.Equals(p, cited, StringComparison.Ordinal) || p.EndsWith("/" + cited, StringComparison.Ordinal))]
            : named;
    }

    private static string BaseName(string path) => path[(path.LastIndexOf('/') + 1)..];

    /// <summary>Splits a 'touches' link's 'path:&lt;repo&gt;/&lt;path&gt;' (a repo name may hold '/').</summary>
    /// <param name="toRef">The link's target.</param>
    /// <returns>The repo's name and the path, or null when no mapped repo owns the prefix.</returns>
    internal (string Repo, string Path)? Touched(string toRef)
    {
        foreach (var name in _longestFirst)
        {
            var prefix = $"path:{name}/";
            if (toRef.StartsWith(prefix, StringComparison.Ordinal))
            {
                return (name, toRef[prefix.Length..]);
            }
        }

        return null;
    }

    /// <summary>Whether a repo's path list holds a path.</summary>
    /// <param name="repo">The repo name.</param>
    /// <param name="path">The path.</param>
    /// <returns>True when it does.</returns>
    internal bool Has(string repo, string path) => _byRepo.TryGetValue(repo, out var paths) && paths.Contains(path);
}
