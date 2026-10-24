using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>One lens being assembled: its found nodes and items numbered L-&lt;lens&gt;-&lt;n&gt;, its edges among them and the anchors, and the sources it searched.</summary>
internal sealed class LensBuilder(string lens, IReadOnlyList<string> anchors)
{
    private readonly HashSet<string> _missing = new(StringComparer.Ordinal);

    private readonly List<JsonObject> _nodes = [];
    private readonly List<JsonObject> _items = [];
    private readonly List<JsonObject> _edges = [];
    private readonly List<JsonObject> _sources = [];
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
    private int _count;

    /// <summary>The lens name.</summary>
    internal string Lens { get; } = lens;

    private string NextId() => string.Create(CultureInfo.InvariantCulture, $"L-{Lens}-{++_count}");

    /// <summary>A found node with a graph key or a file key; a key seen before is not found twice.</summary>
    internal void Node(string? key, string? kind, string? label, string band)
    {
        if (string.IsNullOrEmpty(key) || !_keys.Add(key))
        {
            return;
        }

        _nodes.Add(new JsonObject { ["key"] = key, ["kind"] = kind ?? "node", ["label"] = label ?? key, ["band"] = band, ["lensItem"] = NextId() });
    }

    /// <summary>A found item with no graph key (a record, decision, commit, incident or symbol), with its citations.</summary>
    internal void Item(string kind, string? label, IEnumerable<string> citations) =>
        _items.Add(new JsonObject { ["lensItem"] = NextId(), ["kind"] = kind, ["label"] = label ?? kind, ["citations"] = ScopeJson.ToArray(citations) });

    /// <summary>Found graph nodes read from <paramref name="field"/> of each element.</summary>
    internal void Keyed(IEnumerable<JsonNode> nodes, string field, string band)
    {
        foreach (var node in nodes)
        {
            Node(ScopeJson.Str(node, field), ScopeJson.Str(node, "kind"), ScopeJson.Str(node, "label"), band);
        }
    }

    /// <summary>Found files: a node keyed code:&lt;path&gt; in the code band.</summary>
    internal void Files(IEnumerable<JsonNode> files)
    {
        foreach (var file in files)
        {
            var path = ScopeJson.Str(file, "path");
            Node(path is null ? null : "code:" + path, "code", ScopeJson.Str(file, "label") ?? path, "code");
        }
    }

    /// <summary>Found items citing their key (a record's 'record:' prefix is the context engine's and is dropped).</summary>
    internal void Records(IEnumerable<JsonNode> records, string kind, string keyField, string labelField, string? altLabelField)
    {
        foreach (var record in records)
        {
            var key = ScopeJson.Str(record, keyField);
            Item(kind, ScopeJson.Str(record, labelField) ?? (altLabelField is null ? null : ScopeJson.Str(record, altLabelField)),
                key is null ? [] : [key.StartsWith("record:", StringComparison.Ordinal) ? key["record:".Length..] : key]);
        }
    }

    /// <summary>The symbols a callers or callees list holds, and its state as a source.</summary>
    internal void SymbolItems(JsonNode? symbols)
    {
        Symbols(symbols);
        foreach (var item in ScopeJson.Items(symbols, "items"))
        {
            Item("symbol", ScopeJson.Str(item, "label") ?? ScopeJson.Str(item, "name") ?? ScopeJson.Str(item, "symbol"), ScopeJson.Str(item, "path") is { } path ? ["code:" + path] : []);
        }
    }

    /// <summary>A searched source, once per id, state and reason; null is ignored.</summary>
    internal void Source(JsonObject? source)
    {
        if (source is not null && !_sources.Exists(s => string.Equals(ScopeJson.Str(s, "id"), ScopeJson.Str(source, "id"), StringComparison.Ordinal)
            && string.Equals(ScopeJson.Str(s, "state"), ScopeJson.Str(source, "state"), StringComparison.Ordinal)
            && string.Equals(ScopeJson.Str(s, "reason"), ScopeJson.Str(source, "reason"), StringComparison.Ordinal)))
        {
            _sources.Add((JsonObject)source.DeepClone());
        }
    }

    /// <summary>Several searched sources.</summary>
    internal void Sources(IEnumerable<JsonObject> sources)
    {
        foreach (var source in sources)
        {
            Source(source);
        }
    }

    /// <summary>The symbols source of a callers or callees list or of a code band: its state and reason, as the view says them.</summary>
    internal void Symbols(JsonNode? symbols)
    {
        if (symbols is not null)
        {
            Source(LensGather.Source("symbols", symbols));
        }
    }

    /// <summary>Edges are kept only when both ends are found nodes or anchors, once per ends and type; call after every node is added.</summary>
    internal void Edges(IEnumerable<JsonNode> edges)
    {
        var ends = _nodes.Select(static n => ScopeJson.Str(n, "key")).OfType<string>().Concat(anchors).ToHashSet(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            var from = ScopeJson.Str(edge, "from") ?? string.Empty;
            var to = ScopeJson.Str(edge, "to") ?? string.Empty;
            var type = ScopeJson.Str(edge, "type") ?? "relates";
            if (ends.Contains(from) && ends.Contains(to) && !_edges.Exists(e => Same(e, from, to, type)))
            {
                _edges.Add(new JsonObject
                {
                    ["from"] = from,
                    ["to"] = to,
                    ["type"] = type,
                    ["provenance"] = ScopeJson.Str(edge, "provenance") ?? "deterministic",
                    ["confirmed"] = ScopeJson.Bool(edge, "confirmed") ?? false,
                });
            }
        }
    }

    private static bool Same(JsonObject edge, string from, string to, string type) =>
        string.Equals(ScopeJson.Str(edge, "from"), from, StringComparison.Ordinal) && string.Equals(ScopeJson.Str(edge, "to"), to, StringComparison.Ordinal) && string.Equals(ScopeJson.Str(edge, "type"), type, StringComparison.Ordinal);

    private int Total => _nodes.Count + _items.Count;

    /// <summary>The lens's entry in the lenses fact.</summary>
    internal JsonObject Entry(string mapVersion, int depth, IReadOnlyList<string> hops)
    {
        return new JsonObject
        {
            ["anchors"] = ScopeJson.ToArray(anchors),
            ["depth"] = depth,
            ["hops"] = ScopeJson.ToArray(hops),
            ["mapVersion"] = mapVersion,
            ["foundCount"] = Total,
            ["nodes"] = new JsonArray([.. _nodes.Select(static n => n.DeepClone())]),
            ["edges"] = new JsonArray([.. _edges.Select(static e => e.DeepClone())]),
            ["items"] = new JsonArray([.. _items.Select(static i => i.DeepClone())]),
            ["truncated"] = new JsonArray(),
        };
    }

    /// <summary>Records the asked anchors the view couldn't find: they leave the row's searched anchors and each is an anchor-missing limit.</summary>
    internal void Missing(IEnumerable<string> missing) => _missing.UnionWith(missing.Where(m => anchors.Contains(m, StringComparer.Ordinal)));

    /// <summary>The lens's row in the coverage fact: its state follows what was found and what was recorded as a limit.</summary>
    internal JsonObject Row(string mapVersion, int depth, IReadOnlyList<string> hops)
    {
        var limits = new JsonArray();
        foreach (var source in _sources)
        {
            if (ScopeFixLines.Match(Lens, ScopeJson.Str(source, "state"), ScopeJson.Str(source, "reason")) is { } limit
                && !ScopeJson.Items(limits).Any(l => string.Equals(ScopeJson.Str(l, "why"), limit.Name, StringComparison.Ordinal)))
            {
                ScopeJson.Append(limits, new JsonObject { ["not"] = Not(limit, source), ["why"] = limit.Name });
            }
        }

        foreach (var missing in anchors.Where(_missing.Contains))
        {
            ScopeJson.Append(limits, new JsonObject { ["not"] = missing, ["why"] = "anchor-missing" });
        }

        return new JsonObject
        {
            ["lens"] = Lens,
            ["state"] = Total > 0 ? "found" : limits.Count > 0 ? "not-searched" : "nothing-found",
            ["searched"] = new JsonObject
            {
                ["anchors"] = ScopeJson.ToArray(anchors.Where(a => !_missing.Contains(a))),
                ["sources"] = new JsonArray([.. _sources.Select(static s => s.DeepClone())]),
                ["mapVersion"] = mapVersion,
                ["depth"] = depth,
                ["hops"] = ScopeJson.ToArray(hops),
            },
            ["limits"] = limits,
            ["mapCoverage"] = new JsonObject { ["mapped"] = Total, ["new"] = 0, ["unmapped"] = 0 },
            ["found"] = new JsonObject
            {
                ["count"] = Total,
                ["ids"] = ScopeJson.ToArray(_nodes.Concat(_items).Select(static x => ScopeJson.Str(x, "lensItem")).OfType<string>()),
            },
        };
    }

    private static string Not(RecordedLimit limit, JsonObject source) => limit.Name switch
    {
        "index-not-built" => "callers and callees",
        "history-unlinked" => "tickets linked to the map",
        "commit-not-held" => "the commit's files",
        "not-synced" => "records not yet synced",
        _ => $"the {ScopeJson.Str(source, "id") ?? "source"} source",
    };
}
