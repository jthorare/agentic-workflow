using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Extract;

/// <summary>
/// Derives flows from the repo's journeys (S-FLOW-J): each journey's steps matched in order to a surface or action the map already holds, a test id
/// first, then a route path, then an accessible name. A journey with fewer than two matched steps makes no flow. Each flow is a candidate (confidence
/// 0.8), its 'step-of' edges numbered 1..n. Journey files are not product code: no code node, no 'implements'. Pure and ordered.
/// </summary>
internal static partial class FlowPass
{
    private const double Confidence = 0.8;
    private const string Fallback = "journey";

    /// <summary>Derives the flows.</summary>
    /// <param name="repo">The repo (its name joins the evidence keys).</param>
    /// <param name="journeys">The journey files at the commit.</param>
    /// <param name="found">The surfaces and actions already found.</param>
    /// <returns>The flows and their edges, and what was skipped.</returns>
    internal static (ExtractedMap Map, JourneySkips Skips) Run(ContextRepoName repo, IReadOnlyList<RepoFile> journeys, ExtractedMap found)
    {
        var index = Index(found);
        List<GraphNode> nodes = [];
        List<GraphEdge> edges = [];
        HashSet<string> used = new(StringComparer.Ordinal);
        int unsupported = 0, noMatch = 0, oversize = 0;
        foreach (var file in journeys.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            if (file.Text.Length > Recognizers.MaxFileLength)
            {
                oversize++;
                continue;
            }

            var read = JourneyReader.Read(file);
            unsupported += read.Unsupported;
            if (MapExtractor.Evidence(repo, file.Path) is not { } evidence)
            {
                continue;
            }

            foreach (var journey in read.Journeys)
            {
                var matched = Match(journey, index, ref noMatch);
                if (matched.Count >= 2 && Flow(journey.Title, file.Path, matched, evidence, used) is { } flow)
                {
                    nodes.Add(flow.Node);
                    edges.AddRange(flow.Edges);
                }
            }
        }

        return (ExtractedMap.Empty.Plus(new ExtractedMap(nodes, edges)), new JourneySkips(unsupported, noMatch, Denied: 0, oversize, OverFileCap: 0));
    }

    private static (GraphNode Node, IEnumerable<GraphEdge> Edges)? Flow(string journeyTitle, string path, List<NodeKey> matched, EvidenceRef evidence, HashSet<string> used)
    {
        var title = Title(journeyTitle, path);
        if (!NodeKey.TryFrom("flow:" + Unique(Kebab(title) is { Length: > 0 } k ? k : Fallback, used), out var flow))
        {
            return null;
        }

        return (new GraphNode(flow, title, Provenance.Deterministic, Confidence, [evidence]),
            matched.Select((step, i) => new GraphEdge(step, flow, EdgeType.StepOf, Provenance.Deterministic, 1.0, [evidence], Order: i + 1)));
    }

    // The step nodes in order; a node already in the flow is not repeated (the graph holds one 'step-of' edge per pair).
    private static List<NodeKey> Match(Journey journey, Lookup index, ref int noMatch)
    {
        List<NodeKey> matched = [];
        foreach (var step in journey.Steps)
        {
            var node = index.Find(step);
            if (node is null)
            {
                noMatch++;
            }
            else if (!matched.Exists(m => m.Value == node.Value.Value))
            {
                matched.Add(node.Value);
            }
        }

        return matched;
    }

    private static Lookup Index(ExtractedMap found)
    {
        var steps = found.Nodes.Where(n => n.Key.Kind is NodeKind.Surface or NodeKind.Action).ToList();
        return new Lookup(
            steps.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal),
            [.. steps.Select(n => (n.Key, Path: RoutePath(n.Key.Value, out var method), Method: method)).Where(r => r.Path is not null)],
            steps);
    }

    // 'surface:route/GET:/links' -> '/links', method 'GET'.
    private static string? RoutePath(string key, out string method)
    {
        method = string.Empty;
        const string prefix = "surface:route/";
        if (!key.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var colon = key.IndexOf(':', prefix.Length);
        if (colon < 0)
        {
            return null;
        }

        method = key[prefix.Length..colon];
        return key[(colon + 1)..];
    }

    // A title that carries an email, a URL with credentials or a token-like run is replaced by the file's name: a title is shown and indexed.
    private static string Title(string title, string path)
    {
        var trimmed = title.Trim();
        if (trimmed.Length > 0 && !Secret.IsMatch(trimmed))
        {
            return trimmed;
        }

        var name = path[(path.LastIndexOf('/') + 1)..];
        var stem = name[..(name.IndexOf('.', StringComparison.Ordinal) is var dot and > 0 ? dot : name.Length)];
        return stem.Length > 0 && !Secret.IsMatch(stem) ? stem : Fallback;
    }

    private static string Kebab(string title) => Separators.Replace(title.ToLowerInvariant(), "-").Trim('-');

    private static string Unique(string kebab, HashSet<string> used)
    {
        var key = kebab;
        for (var n = 2; !used.Add(key); n++)
        {
            key = string.Create(CultureInfo.InvariantCulture, $"{kebab}-{n}");
        }

        return key;
    }

    private sealed record Lookup(HashSet<string> Keys, List<(NodeKey Key, string? Path, string Method)> Routes, List<GraphNode> Nodes)
    {
        internal NodeKey? Find(JourneyStep step) => step.Kind switch
        {
            StepKind.TestId => NodeKey.TryFrom("surface:pane/" + step.Value, out var key) && Keys.Contains(key.Value) ? key : null,
            StepKind.Route => Routes.Where(r => string.Equals(r.Path, step.Value, StringComparison.Ordinal))
                .OrderBy(r => r.Method == "GET" ? 0 : 1).ThenBy(r => r.Key.Value, StringComparer.Ordinal).Select(r => (NodeKey?)r.Key).FirstOrDefault(),
            StepKind.Name => Nodes.FirstOrDefault(n => string.Equals(n.Label, step.Value, StringComparison.OrdinalIgnoreCase))?.Key,
            _ => null,
        };
    }

    [GeneratedRegex("[^\\s@]+@[^\\s@]+\\.[^\\s@]+|://[^/\\s]*@|[A-Za-z0-9_-]{24,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Secret { get; }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Separators { get; }
}
