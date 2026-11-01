using Aw.Context.Core.Graph;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linking;

/// <summary>The alias rule (concept pins spec §3.4): a pin's alias, whole and in order, in a record's text links the record to the pin's concept.</summary>
public static partial class RulesLinker
{
    private const double ByAlias = 0.8;

    /// <summary>The alias rule alone (concept pins spec §3.4): the pins' aliases found in one record's text, for the pins whose concept node is in the map. Writes nothing, so a count of what it would link can call it.</summary>
    /// <param name="record">The record the text belongs to.</param>
    /// <param name="text">The sanitized, scrubbed text.</param>
    /// <param name="graph">The map to link into: only pins with a concept:&lt;id&gt; node here are offered.</param>
    /// <param name="pins">The approved pins' ids and aliases.</param>
    /// <returns>The alias links, ordered by target.</returns>
    public static IReadOnlyList<IntentLink> Aliases(RecordKey record, string text, GraphSnapshot graph, IReadOnlyList<PinAliases> pins)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(pins);
        var nodes = graph.Nodes.Where(n => n.Key.Kind == NodeKind.Concept).ToDictionary(n => n.Key.Value, n => n.Key, StringComparer.Ordinal);
        var words = Significant(text);
        var found = new Dictionary<string, IntentLink>(StringComparer.Ordinal);
        foreach (var pin in pins)
        {
            if (!nodes.TryGetValue("concept:" + pin.Id.Value, out var target) || found.ContainsKey(target.Value))
            {
                continue;
            }

            if (pin.Aliases.FirstOrDefault(a => Contains(words, Significant(a))) is { } alias)
            {
                found[target.Value] = new IntentLink(record, target, LinkRule.Alias, ByAlias, Hash(alias));
            }
        }

        return [.. found.Values.OrderBy(l => l.Target.Value, StringComparer.Ordinal)];
    }

    /// <summary>The given words (aliases) the alias rule finds in a text (concept pins spec §3.6): the same match as <see cref="Aliases"/>, for many words at once and on one reading of the text. Writes nothing.</summary>
    /// <param name="text">The sanitized, scrubbed text.</param>
    /// <param name="words">The words to look for; one that normalises to nothing never matches.</param>
    /// <returns>The words found, in the order given.</returns>
    public static IReadOnlyList<string> Matching(string text, IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(words);
        var found = Significant(text);
        return [.. words.Where(w => Contains(found, Significant(w)))];
    }

    // The words of a text as the alias rule reads them: letter and digit runs, lower-case, a plural 's' dropped, small words left out.
    internal static List<string> Significant(string text) =>
        [.. Word.Matches(text).Select(m => AnchorMatch.Significant(m.Value.ToLowerInvariant())).OfType<string>()];

    // An alias that normalises to nothing never matches; one word is enough.
    private static bool Contains(List<string> words, List<string> alias)
    {
        for (var start = 0; alias.Count > 0 && start + alias.Count <= words.Count; start++)
        {
            if (words.GetRange(start, alias.Count).SequenceEqual(alias, StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private sealed partial class Run
    {
        public void Aliases(IReadOnlyList<PinAliases> pins)
        {
            foreach (var link in RulesLinker.Aliases(record, text, graph, pins))
            {
                _ = _found.TryAdd((link.Target.Value, link.Rule), link);
            }
        }
    }
}
