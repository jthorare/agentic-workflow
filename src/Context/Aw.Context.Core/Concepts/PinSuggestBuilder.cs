using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Core.WholeMap;
using Aw.Kernel;

namespace Aw.Context.Core.Concepts;

/// <summary>The pure half of the pin-suggest read (concept pins spec §3.6): a concept's terms, the feature search and the match of a pasted list to concepts. The counts of records need the stores, so they are the service's.</summary>
public static class PinSuggestBuilder
{
    /// <summary>The most terms of a concept the read answers.</summary>
    public const int MaxTerms = 20;

    /// <summary>The features a search answers when the query gives no limit.</summary>
    public const int DefaultLimit = 50;

    /// <summary>The most features a search answers.</summary>
    public const int MaxLimit = 200;

    /// <summary>The terms a concept carries, best first, up to <see cref="MaxTerms"/>, as the build stored them.</summary>
    /// <param name="graph">The graph the caller may see.</param>
    /// <param name="concept">The concept's key.</param>
    /// <returns>The distinct terms, or SND-CONTEXT-003 when the graph has no such concept.</returns>
    public static Result<IReadOnlyList<string>> TermsOf(GraphSnapshot graph, NodeKey concept)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return Find(graph, concept) is { } node
            ? ConceptTermsAttribute.Of(node).Distinct(StringComparer.Ordinal).Take(MaxTerms).ToList()
            : ContextErrors.UnknownAnchor.With("that concept isn't in this map (fix: read the concepts first, and name one of their ids)");
    }

    /// <summary>The rows of a concept's terms: each word labelled like every answer's, with the number of records it would newly link.</summary>
    /// <param name="graph">The graph the caller may see.</param>
    /// <param name="concept">The concept's key.</param>
    /// <param name="text">The caller's text policy.</param>
    /// <param name="words">The terms from <see cref="TermsOf"/>.</param>
    /// <param name="counts">The new records per term, by the term as stored; a term without a count has none.</param>
    /// <returns>One row per term, in the order given.</returns>
    public static IReadOnlyList<PinSuggestTerm> TermRows(GraphSnapshot graph, NodeKey concept, MapText text, IReadOnlyList<string> words, IReadOnlyDictionary<string, int> counts)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(counts);
        var node = Find(graph, concept);
        var trust = node is null ? Results.Trust.Untrusted : TrustPolicy.ForNode(node, node.Provenance == Provenance.Human);
        return [.. words.Select(w => new PinSuggestTerm(text.Label(w, trust), counts.GetValueOrDefault(w)))];
    }

    /// <summary>The features whose label or key holds the text, case-insensitive: labels that start with it first, then by label, then by key.</summary>
    /// <param name="graph">The graph the caller may see.</param>
    /// <param name="text">The caller's text policy.</param>
    /// <param name="search">The text to find.</param>
    /// <param name="limit">The most to answer (1 to <see cref="MaxLimit"/>).</param>
    /// <returns>The features.</returns>
    public static IReadOnlyList<PinSuggestFeature> Search(GraphSnapshot graph, MapText text, string search, int limit)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(search);
        var needle = search.Trim();
        return [.. graph.Nodes
            .Where(n => n.Key.Kind == NodeKind.Feature && (n.Label.Contains(needle, StringComparison.OrdinalIgnoreCase) || n.Key.Value.Contains(needle, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(n => n.Key.Value, StringComparer.Ordinal).Select(g => g.First())
            .OrderBy(n => n.Label.StartsWith(needle, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(n => n.Label, StringComparer.Ordinal)
            .ThenBy(n => n.Key.Value, StringComparer.Ordinal)
            .Take(Math.Clamp(limit, 1, MaxLimit))
            .Select(n => new PinSuggestFeature(n.Key, text.Label(n.Label, TrustPolicy.ForNode(n, n.Provenance == Provenance.Human))))];
    }

    /// <summary>
    /// The concept each line matches. First a concept whose label has the same significant words as the line's name (the name only, never the definition); the larger concept when several do.
    /// Otherwise the concept whose terms share the most distinct significant words with the name and definition (at least one). Ties go to the larger concept, then to the lower key.
    /// 'concept:other' is never offered.
    /// </summary>
    /// <param name="graph">The graph the caller may see.</param>
    /// <param name="text">The caller's text policy.</param>
    /// <param name="lines">The pasted lines.</param>
    /// <returns>One match per line, in order; a line no concept fits has a null concept.</returns>
    public static IReadOnlyList<PinSuggestMatch> Match(GraphSnapshot graph, MapText text, IReadOnlyList<PinSuggestLine> lines)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(lines);
        var known = graph.Nodes.GroupBy(n => n.Key.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var index = ConceptIndex.Of(known, [.. graph.Edges.Where(e => known.ContainsKey(e.From.Value) && known.ContainsKey(e.To.Value))]);
        var candidates = index.Concepts
            .Select(c => new Candidate(c, Words(c.Label), [.. ConceptTermsAttribute.Of(c).SelectMany(Words)], index.MembersOf(c.Key.Value).Count))
            .ToList();
        return [.. lines.Select((line, i) => Chosen(candidates, line) is { } hit
            ? new PinSuggestMatch(i, hit.Node.Key, text.Label(hit.Node.Label, TrustPolicy.ForNode(hit.Node, hit.Node.Provenance == Provenance.Human)))
            : new PinSuggestMatch(i, Concept: null, Name: null))];
    }

    private static Candidate? Chosen(List<Candidate> candidates, PinSuggestLine line)
    {
        var name = Words(line.Name);
        if (name.Count > 0 && Best(candidates.Where(c => c.Label.SetEquals(name)).Select(c => (Candidate: c, Score: 1))) is { } same)
        {
            return same;
        }

        var all = new HashSet<string>(name, StringComparer.Ordinal);
        all.UnionWith(Words(line.Definition ?? string.Empty));
        return Best(candidates.Select(c => (Candidate: c, Score: c.Terms.Count(all.Contains))).Where(s => s.Score > 0));
    }

    // The highest score, then the larger concept, then the lower key.
    private static Candidate? Best(IEnumerable<(Candidate Candidate, int Score)> scored) =>
        scored.OrderByDescending(s => s.Score).ThenByDescending(s => s.Candidate.Size).ThenBy(s => s.Candidate.Node.Key.Value, StringComparer.Ordinal).Select(s => s.Candidate).FirstOrDefault();

    private static HashSet<string> Words(string text) => [.. RulesLinker.Significant(text)];

    private static GraphNode? Find(GraphSnapshot graph, NodeKey concept) =>
        graph.Nodes.FirstOrDefault(n => n.Key.Kind == NodeKind.Concept && n.Key.Value == concept.Value && n.Key.Value != ConceptKeys.Other);

    private sealed record Candidate(GraphNode Node, HashSet<string> Label, HashSet<string> Terms, int Size);
}
