using System.Globalization;
using System.Text.Json;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Graph;

/// <summary>
/// Reads graphify's graph.json as hostile input (spec §5.5): a wrong top-level shape fails the layer (SND-CODEIDX-008, 'shape'), a
/// malformed or out-of-snapshot record is dropped and counted, an unknown relation is skipped, and a graph with over a quarter of
/// its records malformed is not trusted ('untrusted'). Only callable nodes and the 'calls' edges between them are kept.
/// </summary>
internal static class GraphJsonParser
{
    private const int NameLimit = 200;
    private const int EdgesPerNode = 4;

    /// <summary>Parses one graph.json.</summary>
    /// <param name="json">The file's bytes (already under the size cap).</param>
    /// <param name="files">The snapshot's paths; a node naming another is dropped.</param>
    /// <param name="roots">The snapshot root as given and as resolved, to cut an absolute path at.</param>
    /// <param name="maxNodes">The node cap; the edge cap is four times it.</param>
    /// <param name="scrub">Scrubs and cuts a name.</param>
    /// <returns>The checked graph, or SND-CODEIDX-008 with 'shape', 'untrusted' or 'too-large'.</returns>
    public static Result<GraphSnapshot> Parse(ReadOnlyMemory<byte> json, IReadOnlySet<string> files, IReadOnlyList<string> roots, int maxNodes, Func<string, int, string> scrub)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(scrub);
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array
                && root.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array
                ? Read(nodes, links, new GraphPaths(files, roots), scrub, maxNodes)
                : CodeIndexErrors.GraphUnavailable.With("shape");
        }
        catch (JsonException)
        {
            return CodeIndexErrors.GraphUnavailable.With("shape");
        }
    }

    private static Result<GraphSnapshot> Read(JsonElement nodes, JsonElement links, GraphPaths paths, Func<string, int, string> scrub, int maxNodes)
    {
        if (nodes.GetArrayLength() > maxNodes || links.GetArrayLength() > (long)maxNodes * EdgesPerNode)
        {
            return CodeIndexErrors.GraphUnavailable.With("too-large");
        }

        var tally = new Tally();
        var kept = ReadNodes(nodes, paths, scrub, tally);
        var edges = ReadEdges(links, kept, tally);
        var total = nodes.GetArrayLength() + links.GetArrayLength();
        return total > 0 && tally.Malformed * 4 > total
            ? CodeIndexErrors.GraphUnavailable.With("untrusted")
            : new GraphSnapshot([.. kept.Values], edges, tally.Malformed + tally.Dropped);
    }

    private sealed class Tally
    {
        public int Malformed { get; set; }

        public int Dropped { get; set; }
    }

    private sealed record RawNode(string Id, string Label, string SourceFile, int Line, int? Community, bool Callable);

    private static Dictionary<string, GraphNode> ReadNodes(JsonElement nodes, GraphPaths paths, Func<string, int, string> scrub, Tally tally)
    {
        var read = new List<(RawNode Raw, GraphNode Node)>();
        foreach (var element in nodes.EnumerateArray())
        {
            if (ReadRaw(element) is { } raw && paths.Relative(raw.SourceFile) is { } file && ToNode(raw, file, scrub) is { } node)
            {
                read.Add((raw, node));
            }
            else
            {
                tally.Malformed++;
            }
        }

        // NetworkX merges equal ids before writing, so a repeated id means something is wrong: every node with it goes.
        var counts = read.GroupBy(r => r.Raw.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var kept = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        foreach (var (raw, node) in read)
        {
            if (counts[raw.Id] > 1 || (raw.Callable && !paths.InSnapshot(node.File)))
            {
                tally.Dropped++;
            }
            else if (raw.Callable)
            {
                kept[raw.Id] = node;
            }
        }

        return kept;
    }

    private static RawNode? ReadRaw(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || Text(element, "id") is not { Length: > 0 } id
            || Text(element, "label") is not { } label
            || Text(element, "source_file") is not { } sourceFile
            || ParseLine(Text(element, "source_location")) is not { } line)
        {
            return null;
        }

        return Optional(element, "community", out var community) && Flag(element, "_callable", out var callable) && Flag(element, "_callable_class", out var callableClass)
            ? new RawNode(id, label, sourceFile, line, community, callable || callableClass)
            : null;
    }

    private static GraphNode? ToNode(RawNode raw, string file, Func<string, int, string> scrub)
    {
        if (raw.Label.Any(char.IsControl))
        {
            return null;
        }

        var name = raw.Label.EndsWith("()", StringComparison.Ordinal) ? raw.Label[..^2] : raw.Label;
        name = name.StartsWith('.') ? name[1..] : name;
        return name.Length == 0 ? null : new GraphNode(raw.Id, file, scrub(name, NameLimit), raw.Line, raw.Community);
    }

    private static List<GraphEdge> ReadEdges(JsonElement links, Dictionary<string, GraphNode> kept, Tally tally)
    {
        var book = new EdgeBook(kept, tally);
        foreach (var element in links.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object || Text(element, "source") is not { Length: > 0 } source || Text(element, "target") is not { Length: > 0 } target
                || Text(element, "relation") is not { Length: > 0 } relation)
            {
                tally.Malformed++;
            }
            else if (string.Equals(relation, "calls", StringComparison.Ordinal))
            {
                book.AddCall(element, source, target);
            }
        }

        return book.Edges;
    }

    // The call edges kept so far: both ends among the kept nodes, one edge per caller and callee pair.
    private sealed class EdgeBook(Dictionary<string, GraphNode> kept, Tally tally)
    {
        private readonly HashSet<(string, string)> _pairs = [];

        public List<GraphEdge> Edges { get; } = [];

        public void AddCall(JsonElement element, string source, string target)
        {
            if (Text(element, "source_file") is null || ParseLine(Text(element, "source_location")) is not { } line)
            {
                tally.Malformed++;
            }
            else if (!kept.ContainsKey(source) || !kept.ContainsKey(target))
            {
                tally.Dropped++;
            }
            else if (_pairs.Add((source, target)))
            {
                var confidence = string.Equals(Text(element, "confidence"), "EXTRACTED", StringComparison.Ordinal) ? "EXTRACTED" : "INFERRED";
                Edges.Add(new GraphEdge(source, target, "calls", confidence, line));
            }
        }
    }

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Optional(JsonElement element, string name, out int? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var found) || found.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (found.ValueKind == JsonValueKind.Number && found.TryGetInt32(out var number))
        {
            value = number;
            return true;
        }

        return false;
    }

    private static bool Flag(JsonElement element, string name, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(name, out var found))
        {
            return true;
        }

        if (found.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = found.GetBoolean();
            return true;
        }

        return false;
    }

    private static int? ParseLine(string? text)
        => text is { Length: > 1 } && text[0] == 'L' && int.TryParse(text.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var line) && line >= 1 ? line : null;
}
