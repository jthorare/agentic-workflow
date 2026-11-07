namespace Aw.Sindri.Core.Profile;

/// <summary>The built-in 'default' review settings (spec §7.1) and the pure resolver for 'extends' and a repo's pick (§7.2).</summary>
public static class PrReviewDefaults
{
    /// <summary>The name of the built-in settings.</summary>
    public const string DefaultName = "default";

    private static readonly PrReviewGroup[] _groups = [];

    private static readonly string[] _collapse = ["package-lock.json", "yarn.lock", "pnpm-lock.yaml", "**/packages.lock.json", "Cargo.lock", "poetry.lock", "go.sum", "*.min.js", "*.g.cs", "**/generated/**"];

    private static readonly PrReviewRisky[] _risky = [];

    private static readonly PrReviewCheck[] _checks = [];

    /// <summary>The panels, in the order the page shows them; the one place the default order is written.</summary>
    public static IReadOnlyList<string> Panels { get; } = ["requiredChecks", "docMap", "testPlan", "ci", "evidence", "risk", "conversation"];

    /// <summary>The built-in entry: groups by the first two path segments (an empty group list), the lock files and generated code folded, no risky paths, no checks.</summary>
    public static PrReviewSettingsEntry Entry { get; } = new(Panels: Panels, Groups: _groups, Collapse: _collapse, Risky: _risky, Checks: _checks);

    /// <summary>The five keys that carry a source in <see cref="PrReviewResolved.Sources"/>.</summary>
    public static IReadOnlyList<string> Keys { get; } = ["panels", "groups", "collapse", "risky", "checks"];

    /// <summary>Resolves <paramref name="name"/> against <paramref name="review"/>: its own keys, else its 'extends' chain's, else the built-in. Lists replace.</summary>
    /// <param name="review">The review block, or null (only 'default' resolves).</param>
    /// <param name="name">'default' or an entry name.</param>
    /// <returns>The resolved settings, or null when the name or a link of its chain is unknown, or the chain loops.</returns>
    public static PrReviewResolved? Resolve(PrReviewSettings? review, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var chain = Chain(review, name);
        if (chain is null)
        {
            return null;
        }

        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        var panels = Pick(chain, static e => e.Panels, Panels, "panels", sources);
        var groups = Pick(chain, static e => e.Groups, _groups, "groups", sources);
        var collapse = Pick(chain, static e => e.Collapse, _collapse, "collapse", sources);
        var risky = Pick(chain, static e => e.Risky, _risky, "risky", sources);
        var checks = Pick(chain, static e => e.Checks, _checks, "checks", sources);
        return new PrReviewResolved(name, panels, groups, collapse, risky, checks, sources);
    }

    /// <summary>The settings a PR of <paramref name="repo"/> gets: the one project entry naming the repo, several (ambiguous), or none ('default').</summary>
    /// <param name="review">The review block, or null.</param>
    /// <param name="repo">owner/name.</param>
    /// <returns>The pick.</returns>
    public static PrReviewPick For(PrReviewSettings? review, string repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var named = (review?.Projects ?? []).Where(p => (p.Repos ?? []).Contains(repo, StringComparer.OrdinalIgnoreCase)).ToList();
        return named.Count switch
        {
            0 => new PrReviewPick(Resolve(review, DefaultName), Choices: []),
            1 => new PrReviewPick(Resolve(review, named[0].Settings ?? DefaultName), Choices: []),
            _ => new PrReviewPick(Settings: null, named),
        };
    }

    /// <summary>The entries from <paramref name="name"/> up its 'extends' chain, nearest first, ending before the built-in; null for an unknown name or a cycle.</summary>
    /// <param name="review">The review block, or null.</param>
    /// <param name="name">The entry to start from.</param>
    /// <returns>The chain as (name, entry) pairs, or null.</returns>
    public static IReadOnlyList<(string Name, PrReviewSettingsEntry Entry)>? Chain(PrReviewSettings? review, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        List<(string Name, PrReviewSettingsEntry Entry)> chain = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        var current = (string?)name;
        while (current is not null and not DefaultName)
        {
            if (!seen.Add(current) || review?.Settings is not { } all || !all.TryGetValue(current, out var entry))
            {
                return null;
            }

            chain.Add((current, entry));
            current = entry.Extends;
        }

        return chain;
    }

    private static IReadOnlyList<T> Pick<T>(IReadOnlyList<(string Name, PrReviewSettingsEntry Entry)> chain, Func<PrReviewSettingsEntry, IReadOnlyList<T>?> key, IReadOnlyList<T> builtIn, string label, Dictionary<string, string> sources)
    {
        foreach (var (name, entry) in chain)
        {
            if (key(entry) is { } list)
            {
                sources[label] = name;
                return list;
            }
        }

        sources[label] = DefaultName;
        return builtIn;
    }
}
