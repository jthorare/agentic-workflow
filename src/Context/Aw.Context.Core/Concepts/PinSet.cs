using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Kernel;

namespace Aw.Context.Core.Concepts;

/// <summary>
/// Every pin the caller can see, with the node and path each one holds (concept pins spec §3.1). Ids are made over the mapped repos, so
/// they are the map's own; a node is in the pin its home feature belongs to, and when two pins claim it the first in pin order keeps it.
/// </summary>
public sealed class PinSet
{
    private readonly IReadOnlyList<ResolvedPin> _all;
    private readonly Dictionary<string, IReadOnlyList<ResolvedPin>> _byRepo;
    private readonly Dictionary<string, int> _holder;   // node key -> rank of the pin that holds it
    private readonly HashSet<string> _concepts;         // concept node keys the map has

    private PinSet(IReadOnlyList<ResolvedPin> all, IReadOnlyList<ConceptId> visible, Dictionary<string, IReadOnlyList<ResolvedPin>> byRepo, Dictionary<string, int> holder, HashSet<string> concepts)
    {
        (_all, Visible, _byRepo, _holder, _concepts) = (all, visible, byRepo, holder, concepts);
    }

    /// <summary>No pins.</summary>
    public static PinSet Empty { get; } = new([], [], new Dictionary<string, IReadOnlyList<ResolvedPin>>(StringComparer.Ordinal), new Dictionary<string, int>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));

    /// <summary>True when the caller sees no pin.</summary>
    public bool IsEmpty => Visible.Count == 0;

    /// <summary>The visible pins' ids, in the order the map builds them.</summary>
    public IReadOnlyList<ConceptId> Visible { get; }

    /// <summary>The pins of a map.</summary>
    /// <param name="mapped">The repos the map is built from; the ids are made over these.</param>
    /// <param name="visible">The caller's repos; a pin that applies to none of them is not visible.</param>
    /// <param name="graph">The map's graph.</param>
    /// <returns>The set.</returns>
    public static PinSet Of(IReadOnlyList<ContextRepo> mapped, IReadOnlyList<ContextRepo> visible, GraphSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(mapped);
        ArgumentNullException.ThrowIfNull(visible);
        ArgumentNullException.ThrowIfNull(graph);
        var resolved = PinIds.Resolve(mapped.SelectMany(r => r.Concepts));
        if (resolved.Count == 0)
        {
            return Empty;
        }

        var byRepo = mapped.ToDictionary(r => r.Name.Value, r => PinIds.Of(resolved, r.Concepts), StringComparer.Ordinal);
        var seen = visible.SelectMany(r => byRepo.TryGetValue(r.Name.Value, out var own) ? own : []).Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var ids = resolved.Where(p => seen.Contains(p.Id)).Select(p => ConceptId.From(p.Id)).ToList();
        return new PinSet(resolved, ids, byRepo, Holders(resolved, mapped, graph), [.. graph.Nodes.Where(n => n.Key.Kind == NodeKind.Concept).Select(n => n.Key.Value)]);
    }

    /// <summary>The pin a node sits in: the first in pin order when several claim it.</summary>
    /// <param name="key">The node.</param>
    /// <returns>The pin, or null.</returns>
    public ConceptId? PinOf(NodeKey key) => _holder.TryGetValue(key.Value, out var rank) ? ConceptId.From(_all[rank].Id) : null;

    /// <summary>The scope of one pin the caller can see.</summary>
    /// <param name="pin">A pin id, or 'concept:&lt;id&gt;'.</param>
    /// <param name="mapTag">How answers name the map ('v41').</param>
    /// <returns>The scope, or SND-CONTEXT-102, 103 or 104.</returns>
    public Result<PinScope> Scope(string pin, string mapTag)
    {
        ArgumentNullException.ThrowIfNull(pin);
        ArgumentNullException.ThrowIfNull(mapTag);
        var wanted = pin.Trim();
        wanted = wanted.StartsWith("concept:", StringComparison.Ordinal) ? wanted["concept:".Length..] : wanted;
        if (Visible.Count == 0)
        {
            return ContextErrors.NoPins.With("no pins are set up (fix: add index.concepts to the profile)");
        }

        var matches = Visible.Where(id => string.Equals(id.Value, wanted, StringComparison.Ordinal)).ToList();
        if (matches.Count == 0)
        {
            var near = Visible.Select(id => id.Value).OrderBy(id => Distance(id, wanted)).ThenBy(id => id, StringComparer.Ordinal).Take(MaxSuggestions).ToList();
            return ContextErrors.PinUnknown.With($"no pin is named '{Printable(wanted)}' (fix: try {string.Join(", ", near)})");
        }

        var found = matches[0];
        return _concepts.Contains("concept:" + found.Value)
            ? new PinScope(this, found)
            : ContextErrors.PinNewerThanMap.With($"pin '{found.Value}' is newer than map {mapTag} (fix: sindri context map build)");
    }

    /// <summary>The pin a repo file is in: the first of that repo's pins whose paths match it.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>The pin's id, or null.</returns>
    internal string? PathPin(ContextRepoName repo, string path) =>
        _byRepo.TryGetValue(repo.Value, out var pins) ? pins.FirstOrDefault(p => PathDenylist.MatchesExactCase(path, p.Pin.Paths))?.Id : null;

    private const int MaxSuggestions = 5;

    // The map's own grouping: a concept holds itself, a feature belongs-to its concept, a code file implements its home feature, a record mentions the concept or a member feature.
    private static Dictionary<string, int> Holders(IReadOnlyList<ResolvedPin> resolved, IReadOnlyList<ContextRepo> mapped, GraphSnapshot graph)
    {
        var rank = resolved.Select((p, i) => (p.Id, i)).ToDictionary(x => "concept:" + x.Id, x => x.i, StringComparer.Ordinal);
        var holder = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, i) in rank)
        {
            Claim(holder, key, i);
        }

        foreach (var edge in graph.Edges.Where(e => e.Type is EdgeType.BelongsTo or EdgeType.Mentions && rank.ContainsKey(e.To.Value)))
        {
            Claim(holder, edge.From.Value, rank[edge.To.Value]);
        }

        var members = holder.Where(h => h.Key.StartsWith("feature:", StringComparison.Ordinal)).ToDictionary(h => h.Key, h => h.Value, StringComparer.Ordinal);
        foreach (var edge in graph.Edges.Where(e => e.Type == EdgeType.Mentions && members.ContainsKey(e.To.Value)))
        {
            Claim(holder, edge.From.Value, members[edge.To.Value]);
        }

        foreach (var edge in graph.Edges.Where(e => e is { Type: EdgeType.Implements, From.Kind: NodeKind.Code, To.Kind: NodeKind.Feature } && members.ContainsKey(e.To.Value)))
        {
            if (mapped.Any(r => HomeFeatures.IsHomeKey(r.Name, edge.To)))
            {
                Claim(holder, edge.From.Value, members[edge.To.Value]);
            }
        }

        return holder;
    }

    private static void Claim(Dictionary<string, int> holder, string key, int rank) =>
        holder[key] = holder.TryGetValue(key, out var existing) ? Math.Min(existing, rank) : rank;

    private static string Printable(string text) => text.Length <= 64 && text.All(c => !char.IsControl(c)) ? text : "(a name with unusual characters)";

    // Levenshtein distance, case-folded: the "nearest ids" of an unknown name.
    private static int Distance(string a, string b)
    {
        var (x, y) = (a.ToLowerInvariant(), b.ToLowerInvariant());
        var row = Enumerable.Range(0, y.Length + 1).ToArray();
        for (var i = 1; i <= x.Length; i++)
        {
            var diagonal = row[0];
            row[0] = i;
            for (var j = 1; j <= y.Length; j++)
            {
                var above = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (x[i - 1] == y[j - 1] ? 0 : 1));
                diagonal = above;
            }
        }

        return row[y.Length];
    }
}
