using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Graph;

/// <summary>
/// Resolves what a caller names to graph nodes (spec §8.1). The 'node' shape takes a key, an alias or one exact label
/// (case ignored). When that names nothing and the label is not shared by several nodes, one feature still matches when
/// every significant word of the name (two or more, each of length at least 3) sits in its label, in any order and with
/// punctuation ignored. The 'text' shape anchors on nodes whose whole label the question uses: words compared
/// lower-case, a plural 's' dropped, small words and a parenthesized kind ('(command)') ignored. The most specific
/// match wins (a label whose words are all in another match's label gives way to it). Suggestions list nodes that share
/// those words, most first, then a test file (its key or label contains '/__tests__/', '.test.', '.spec.' or 'fixture')
/// after every other node with the same count. Source-hit voting waits for sources that link records to nodes (S-5B).
/// </summary>
public static class AnchorMatch
{
    private const int MaxSuggestions = 5;

    /// <summary>The most keys an error's extension list holds (the host cuts longer arrays).</summary>

    /// <summary>The longest key a typed list carries: the HTTP problem writer cuts longer strings, and a cut key is no key (it stays in the detail).</summary>
    private const int MaxListedKey = 200;

    private static readonly HashSet<string> _small = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "can", "do", "does", "done", "for", "from", "how", "i", "if", "in", "is", "it", "its", "of", "on",
        "or", "s", "the", "this", "that", "to", "was", "what", "when", "where", "which", "who", "why", "with",
    };

    /// <summary>The node <paramref name="name"/> names: a key, then an alias, then a label (case ignored).</summary>
    /// <param name="graph">The graph a caller may see.</param>
    /// <param name="name">A node key, alias or label.</param>
    /// <param name="repos">The repos in scope; a demoted file's code node is never suggested. Null suggests every node.</param>
    /// <returns>The node; SND-CONTEXT-037 when a label names several (the message lists them); SND-CONTEXT-003 with suggestions when nothing matches.</returns>
    public static Result<GraphNode> Resolve(GraphSnapshot graph, string name, IReadOnlyList<ContextRepo>? repos = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(name);
        var trimmed = name.Trim();
        if (NodeKey.TryFrom(trimmed, out var key) && graph.Resolve(key) is { } byKey)
        {
            return byKey;
        }

        var byLabel = graph.Nodes.Where(n => string.Equals(n.Label.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.Key.Value, StringComparer.Ordinal).ToList();
        if (byLabel.Count == 1)
        {
            return byLabel[0];
        }

        if (byLabel.Count > 1)
        {
            return ContextErrors.AmbiguousAnchor
                .With($"'{Shown(trimmed)}' is the name of {byLabel.Count} nodes: {string.Join(", ", byLabel.Select(n => n.Key.Value))} (fix: ask by key)")
                .WithExtension("candidates", [.. byLabel.Select(n => n.Key.Value).Where(Listable)], SettingsJsonContext.Default.ListString);
        }

        if (NearFeature(graph, trimmed) is { } near)
        {
            return near;
        }

        var suggested = Suggest(graph, Words(trimmed), except: [], repos ?? []);
        var hint = suggested.Count == 0 ? "sindri context map status" : "try " + string.Join(", ", suggested.Select(k => k.Value));
        var unknown = ContextErrors.UnknownAnchor.With($"no node is named '{Shown(trimmed)}' (fix: {hint})");
        return suggested.Count == 0
            ? unknown
            : unknown.WithExtension("suggestedAnchors", [.. suggested.Select(k => k.Value).Where(Listable)], SettingsJsonContext.Default.ListString);
    }

    /// <summary>The nodes a question names by their whole label, and others it comes close to.</summary>
    /// <param name="graph">The graph a caller may see.</param>
    /// <param name="question">The question.</param>
    /// <returns>The anchors and suggestions.</returns>
    public static TextAnchors FromText(GraphSnapshot graph, string question)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(question);
        var words = Words(question);
        var whole = graph.Nodes.Select(n => (Node: n, Words: Words(Unbracketed(n.Label))))
            .Where(m => m.Words.Count > 0 && m.Words.IsSubsetOf(words)).ToList();
        var anchors = whole.Where(m => !whole.Exists(o => o.Words.Count > m.Words.Count && m.Words.IsSubsetOf(o.Words)))
            .OrderByDescending(m => m.Words.Count).ThenBy(m => m.Node.Key.Value, StringComparer.Ordinal).Select(m => m.Node.Key).ToList();
        return new TextAnchors(anchors, Suggest(graph, words, [.. whole.Select(m => m.Node.Key.Value)], []));
    }

    /// <summary>The most words of a question matching reads (raised from 256 per owner q93, 2026-10-23).</summary>
    internal const int MaxWords = 4_000;

    /// <summary>The words of a text as matching reads them: lower-case letter and digit runs, small words left out before and after a plural 's' is dropped ('this' is never 'thi').</summary>
    /// <param name="text">The text.</param>
    /// <returns>The words.</returns>
    internal static HashSet<string> Words(string text) =>
        QueryRules.Words(text, MaxWords).Where(w => !_small.Contains(w)).Select(Singular).Where(w => !_small.Contains(w)).ToHashSet(StringComparer.Ordinal);

    private static GraphNode? NearFeature(GraphSnapshot graph, string name)
    {
        var words = Words(name).Where(w => w.Length >= 3).ToHashSet(StringComparer.Ordinal);
        if (words.Count < 2)
        {
            return null;
        }

        var hits = graph.Nodes.Where(n => n.Key.Kind == NodeKind.Feature && words.IsSubsetOf(Words(Unbracketed(n.Label)))).Take(2).ToList();
        return hits.Count == 1 ? hits[0] : null;
    }

    private static IReadOnlyList<NodeKey> Suggest(GraphSnapshot graph, HashSet<string> words, IReadOnlyCollection<string> except, IReadOnlyList<ContextRepo> repos) =>
        [.. graph.Nodes.Where(n => !except.Contains(n.Key.Value, StringComparer.Ordinal) && !DemotedCode.IsDemoted(n.Key, repos))
            .Select(n => (n.Key, n.Label, Shared: Words(Unbracketed(n.Label)).Count(w => w.Length >= 3 && words.Contains(w))))
            .Where(m => m.Shared > 0)
            .OrderByDescending(m => m.Shared).ThenBy(m => TestLike(m.Key.Value) || TestLike(m.Label)).ThenBy(m => m.Key.Value, StringComparer.Ordinal)
            .Take(MaxSuggestions).Select(m => m.Key)];

    private static bool TestLike(string text) =>
        text.Contains("/__tests__/", StringComparison.OrdinalIgnoreCase)
        || text.Contains(".test.", StringComparison.OrdinalIgnoreCase)
        || text.Contains(".spec.", StringComparison.OrdinalIgnoreCase)
        || text.Contains("fixture", StringComparison.OrdinalIgnoreCase);

    private static string Singular(string word) =>
        word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal) ? word[..^1] : word;

    /// <summary>A label without its parenthesized parts ('Split vertically (command)' → 'Split vertically').</summary>
    private static string Unbracketed(string label)
    {
        var open = label.IndexOf('(', StringComparison.Ordinal);
        return open <= 0 ? label : label[..open];
    }

    private static string Shown(string text) => text.Length <= 80 && !text.Any(char.IsControl) ? text : "(a long or unusual name)";

    private static bool Listable(string key) => key.Length <= MaxListedKey;
}
