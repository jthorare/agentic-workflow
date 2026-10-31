using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Kernel;

namespace Aw.Context.Core.Concepts;

/// <summary>
/// Every pin the caller can see, with the node and path each one holds (concept pins spec §3.1). Ids are made over the mapped repos, so
/// they are the map's own; a node is in the pin its home feature belongs to, and when two pins claim it the first in pin order keeps it.
/// </summary>
public sealed class PinSet : IAliasPins
{
    private readonly IReadOnlyList<ResolvedPin> _all;
    private readonly Dictionary<string, IReadOnlyList<ResolvedPin>> _byRepo;
    private readonly Dictionary<string, int> _holder;   // node key -> rank of the pin that holds it
    private readonly HashSet<string> _concepts;         // concept node keys the map has
    private readonly Dictionary<string, ResolvedPin> _shown;   // the visible pins by id
    private readonly List<(string Id, string Alias, HashSet<string> Words)> _aliases;   // visible pins' aliases, as the matcher reads them

    private PinSet(IReadOnlyList<ResolvedPin> all, IReadOnlyList<ConceptId> visible, Dictionary<string, IReadOnlyList<ResolvedPin>> byRepo, Dictionary<string, int> holder, HashSet<string> concepts)
    {
        (_all, Visible, _byRepo, _holder, _concepts) = (all, visible, byRepo, holder, concepts);
        var ids = visible.Select(v => v.Value).ToHashSet(StringComparer.Ordinal);
        _shown = all.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, StringComparer.Ordinal);
        _aliases = [.. _shown.Values.SelectMany(p => p.Pin.Aliases.Select(a => (p.Id, Alias: a, Words: AnchorMatch.Words(a)))).Where(a => a.Words.Count > 0)];
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
    public static PinSet Of(IReadOnlyList<ContextRepo> mapped, IReadOnlyList<ContextRepo> visible, GraphSnapshot graph) => Of(mapped, visible, graph, graph);

    /// <summary>The pins of a map the caller reads: what each holds comes from the graph the caller sees, whether the map has the pin yet from the stored one.</summary>
    /// <param name="mapped">The repos the map is built from; the ids are made over these.</param>
    /// <param name="visible">The caller's repos; a pin that applies to none of them is not visible.</param>
    /// <param name="graph">The graph the caller sees (the owner's renames, merges and moves applied, the synced record links merged in).</param>
    /// <param name="stored">The stored graph of the map version, whose concept nodes say which pins the map has been built with.</param>
    /// <returns>The set.</returns>
    public static PinSet Of(IReadOnlyList<ContextRepo> mapped, IReadOnlyList<ContextRepo> visible, GraphSnapshot graph, GraphSnapshot stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
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
        return new PinSet(resolved, ids, byRepo, Holders(resolved, mapped, graph), [.. stored.Nodes.Where(n => n.Key.Kind == NodeKind.Concept).Select(n => n.Key.Value)]);
    }

    /// <summary>The pin whose alias is exactly <paramref name="name"/> (compared as the matcher reads words: lower case, singular, small words left out).</summary>
    /// <param name="name">The name.</param>
    /// <returns>The pin's id, or null.</returns>
    public ConceptId? ByAlias(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var words = AnchorMatch.Words(name);
        return words.Count == 0 || _aliases.Find(a => a.Words.SetEquals(words)) is not { Id: { } id } ? null : ConceptId.From(id);
    }

    /// <summary>Every alias whose words all sit in <paramref name="words"/>, the longest first.</summary>
    /// <param name="words">The words of a question, as <c xml:lang="en">AnchorMatch.Words</c> reads them.</param>
    /// <returns>Each match's pin and the alias as the owner wrote it.</returns>
    public IReadOnlyList<(string Pin, string Alias, int Words)> AliasesIn(IReadOnlySet<string> words) =>
        [.. _aliases.Where(a => a.Words.IsSubsetOf(words)).OrderByDescending(a => a.Words.Count).ThenBy(a => a.Alias, StringComparer.Ordinal).Select(a => (a.Id, a.Alias, a.Words.Count))];

    /// <inheritdoc />
    string? IAliasPins.PinByAlias(string name) => ByAlias(name)?.Value;

    /// <summary>Whether a concept is an owner's pin the caller sees.</summary>
    /// <param name="concept">The concept's key ('concept:billing').</param>
    /// <returns>True for a pin.</returns>
    public bool IsPinned(NodeKey concept) => IdOf(concept) is { } id && _shown.ContainsKey(id);

    /// <summary>The aliases the owner gave a pin.</summary>
    /// <param name="concept">The concept's key.</param>
    /// <returns>The aliases, empty for a concept that is no pin.</returns>
    public IReadOnlyList<string> AliasesOf(NodeKey concept) => IdOf(concept) is { } id && _shown.TryGetValue(id, out var pin) ? pin.Pin.Aliases : [];

    /// <summary>A pin's definition, read from the approved profile (not from the map node, which carries the one of its last build).</summary>
    /// <param name="concept">The concept's key.</param>
    /// <returns>The summary, or null when the concept is no pin or it has none.</returns>
    public string? SummaryOf(NodeKey concept) => IdOf(concept) is { } id && _shown.TryGetValue(id, out var pin) && !string.IsNullOrWhiteSpace(pin.Pin.Summary) ? pin.Pin.Summary : null;

    private static string? IdOf(NodeKey concept) => concept.Kind == NodeKind.Concept && concept.Value.StartsWith("concept:", StringComparison.Ordinal) ? concept.Value["concept:".Length..] : null;

    /// <summary>The pin a node sits in: the first in pin order when several claim it.</summary>
    /// <param name="key">The node.</param>
    /// <returns>The pin, or null.</returns>
    public ConceptId? PinOf(NodeKey key) => _holder.TryGetValue(key.Value, out var rank) ? ConceptId.From(_all[rank].Id) : null;

    /// <summary>The pin a node sits in, when the caller can see it; a pin they can't is none.</summary>
    internal ConceptId? VisiblePinOf(NodeKey key) => PinOf(key) is { } pin && IsVisible(pin) ? pin : null;

    /// <summary>The visible pin a repo file's path sits in, or null.</summary>
    internal ConceptId? VisiblePathPin(ContextRepoName repo, string path) => PathPin(repo, path) is { } id && ConceptId.From(id) is var pin && IsVisible(pin) ? pin : null;

    /// <summary>The keys the pin holds that start with a prefix ('record:', 'code:').</summary>
    internal IEnumerable<string> KeysHeldBy(ConceptId pin, string prefix)
    {
        var rank = -1;
        for (var i = 0; i < _all.Count; i++)
        {
            if (string.Equals(_all[i].Id, pin.Value, StringComparison.Ordinal))
            {
                rank = i;
            }
        }

        return _holder.Where(h => h.Value == rank && h.Key.StartsWith(prefix, StringComparison.Ordinal)).Select(h => h.Key);
    }

    private bool IsVisible(ConceptId pin) => Visible.Any(v => string.Equals(v.Value, pin.Value, StringComparison.Ordinal));

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
        if (matches.Count == 0 && ByAlias(wanted) is { } byAlias)
        {
            matches = [byAlias];
        }

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

    /// <summary>The pin a repo document's canonical key ('repo:&lt;repo&gt;/&lt;path&gt;') is in, by its path.</summary>
    /// <param name="recordKey">The record's canonical key.</param>
    /// <returns>The pin's id, or null for any other key or a path no pin matches.</returns>
    internal string? RecordPathPin(string recordKey)
    {
        const string prefix = "repo:";
        if (!recordKey.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = recordKey[prefix.Length..];
        return _byRepo.Keys.Where(r => rest.StartsWith(r + "/", StringComparison.Ordinal)).OrderByDescending(r => r.Length)
            .Select(r => PathPin(ContextRepoName.From(r), rest[(r.Length + 1)..])).FirstOrDefault(p => p is not null);
    }

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
