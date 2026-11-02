using System.Globalization;
using System.Text;
using Aw.Context.Core.Glossary;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.ConceptScan;

/// <summary>The compact text of a draft map the first model pass reads: one line per feature with its label and top files, then the glossary's terms when the code index has them.</summary>
public static class ConceptScanSummary
{
    private const int FilesPerFeature = 4;
    private const int TermsShown = 40;
    private const int FeaturesPerTerm = 3;

    /// <summary>The keys of the graph's features, in key order.</summary>
    /// <param name="graph">The draft's graph.</param>
    /// <returns>The keys ('feature:…'), distinct.</returns>
    public static IReadOnlyList<string> FeatureKeys(GraphSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return [.. graph.Nodes.Where(static n => n.Key.Kind == NodeKind.Feature).Select(static n => n.Key.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    /// <summary>Writes the summary, cut at <paramref name="maxChars"/> at a line end.</summary>
    /// <param name="graph">The draft's graph.</param>
    /// <param name="glossary">The glossary of the map's features, or null when the code index has none.</param>
    /// <param name="maxChars">The most characters the text may have.</param>
    /// <returns>The text; a feature that did not fit is counted in its last line.</returns>
    public static string Build(GraphSnapshot graph, MapGlossary? glossary, int maxChars)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var files = graph.Edges
            .Where(static e => e.Type == EdgeType.Implements && e.From.Kind == NodeKind.Code && e.To.Kind == NodeKind.Feature)
            .GroupBy(static e => e.To.Value, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.Select(static e => e.From.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(FilesPerFeature).ToList(), StringComparer.Ordinal);
        var labels = graph.Nodes.Where(static n => n.Key.Kind == NodeKind.Feature).GroupBy(static n => n.Key.Value, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.First().Label, StringComparer.Ordinal);
        var lines = FeatureKeys(graph).Select(key => Line(key, Plain(labels[key]), files.GetValueOrDefault(key) ?? [])).ToList();
        lines.AddRange(Terms(glossary));
        return Cut(lines, maxChars);
    }

    private static string Line(string key, string label, List<string> files) =>
        files.Count == 0 ? key + " | " + label : key + " | " + label + " | " + string.Join(", ", files);

    private static IEnumerable<string> Terms(MapGlossary? glossary)
    {
        if (glossary is null || glossary.Terms.Count == 0)
        {
            yield break;
        }

        yield return "glossary terms (words that set groups of features apart):";
        foreach (var term in glossary.Terms.Take(TermsShown))
        {
            yield return "term " + term.Term + ": " + string.Join(", ", term.Features.Take(FeaturesPerTerm).Select(static f => f.Value));
        }
    }

    private static string Cut(List<string> lines, int maxChars)
    {
        var text = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            if (text.Length + lines[i].Length + 1 > maxChars - 40)
            {
                _ = text.Append(string.Create(CultureInfo.InvariantCulture, $"+{lines.Count - i} more lines not shown"));
                break;
            }

            _ = text.AppendLine(lines[i]);
        }

        return text.ToString();
    }

    private static string Plain(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
