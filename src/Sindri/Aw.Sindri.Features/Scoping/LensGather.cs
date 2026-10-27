using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// The one mapping from the context engine's view JSON to the scoping facts (scoping spec §5.2, C1, C3, C5): LensesView (schema 1 or 2),
/// CodeView and HistoryView in, the lenses fact and the coverage fact out. The desk calls it over files a person hands it, and the block
/// runtime over its Step inputs' results, so both produce identical facts. Deterministic, no I/O.
/// <list type="bullet">
/// <item>product: the product band's own nodes; related: the cascade's neighbors and affects, plus the intent band's records and decisions as items;</item>
/// <item>code: the code files (LensesView's and CodeView's), tests, schema files, the things the code reads and the callers and callees; history: HistoryView's records, decisions, changes and incidents.</item>
/// </list>
/// A view that was not handed over is a recorded limit (not-configured) for the lens it feeds, except the lenses view, which the caller requires.
/// </summary>
internal static class LensGather
{
    /// <summary>Maps the views into the lenses fact and the coverage fact. The anchors are the requested ones; when none are given the lenses view's are used.</summary>
    internal static LensGatherResult Assemble(JsonNode? lenses, JsonNode? code, JsonNode? history, IReadOnlyList<string> anchors)
    {
        var asked = anchors.Count > 0 ? anchors : ViewAnchors(lenses);
        var version = new[] { lenses, code, history }.Select(static v => ScopeJson.Str(v, "mapVersion")).FirstOrDefault(static v => !string.IsNullOrEmpty(v)) ?? "unknown";
        var set = new[] { Product(lenses, asked), Related(lenses, asked), CodeLens(lenses, code, asked), HistoryLens(history, asked) };
        MarkMissing(set, lenses, code, history);

        var entries = new JsonObject();
        var rows = new JsonArray();
        foreach (var lens in ScopeRules.Lenses)
        {
            var built = set.Single(b => string.Equals(b.Lens, lens, StringComparison.Ordinal));
            var depth = string.Equals(lens, "related", StringComparison.Ordinal) ? ScopeJson.Int(lenses?["searched"], "depth") ?? ScopeJson.Int(lenses, "depth") ?? 0 : 0;
            var hops = string.Equals(lens, "related", StringComparison.Ordinal) ? ScopeJson.Strings(lenses?["searched"], "hops").ToList() : [];
            ScopeJson.Append(rows, built.Row(version, depth, hops));
            entries[lens] = built.Entry(version, depth, hops);
        }

        return new LensGatherResult(entries, new JsonObject { ["lenses"] = rows });
    }

    // Each lens gets the anchors the views it reads couldn't find: product and related from the lenses view, code from it and the code view, history from the history view.
    private static void MarkMissing(LensBuilder[] set, JsonNode? lenses, JsonNode? code, JsonNode? history)
    {
        foreach (var built in set)
        {
            built.Missing(built.Lens switch
            {
                "code" => [.. Missing(lenses), .. Missing(code)],
                "history" => Missing(history),
                _ => Missing(lenses),
            });
        }
    }

    // The anchors a view says it couldn't find: each is a recorded limit of the lenses that view feeds, not a searched anchor.
    private static List<string> Missing(JsonNode? view) => [.. ScopeJson.Strings(view, "missingAnchors")];

    private static List<string> ViewAnchors(JsonNode? lenses) =>
        [.. (ScopeJson.Arr(lenses, "anchors") is { } many ? ScopeJson.Items(many).Select(static a => ScopeJson.Str(a, "key")) : [ScopeJson.Str(lenses?["anchor"], "key")]).OfType<string>()];

    private static LensBuilder Product(JsonNode? lenses, IReadOnlyList<string> anchors)
    {
        var lens = new LensBuilder("product", anchors);
        var product = lenses?["bands"]?["product"];
        lens.Keyed(ScopeJson.Items(product, "own"), "node", "product");
        lens.Source(MapSource(lenses));
        lens.Edges(ScopeJson.Items(product, "edges"));
        return lens;
    }

    private static LensBuilder Related(JsonNode? lenses, IReadOnlyList<string> anchors)
    {
        var lens = new LensBuilder("related", anchors);
        var product = lenses?["bands"]?["product"];
        var own = ScopeJson.Items(product, "own").Select(static n => ScopeJson.Str(n, "node")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        lens.Keyed(ScopeJson.Items(product, "neighbors").Concat(ScopeJson.Items(product, "affects")).Where(n => !own.Contains(ScopeJson.Str(n, "node") ?? string.Empty)), "node", "product");
        var intent = lenses?["bands"]?["intent"];
        lens.Records(ScopeJson.Items(intent, "records"), "record", "key", "label", altLabelField: null);
        lens.Records(ScopeJson.Items(intent, "decisions"), "decision", "node", "label", "text");
        lens.Source(MapSource(lenses));
        lens.Sources(Sources(lenses).Where(static s => !IsMapOrCodeIndex(ScopeJson.Str(s, "id"))));
        lens.Edges(ScopeJson.Items(product, "edges"));
        return lens;
    }

    private static LensBuilder CodeLens(JsonNode? lenses, JsonNode? code, IReadOnlyList<string> anchors)
    {
        var lens = new LensBuilder("code", anchors);
        var band = lenses?["bands"]?["code"];
        var files = ScopeJson.Items(band, "files").Concat(ScopeJson.Items(code, "files"));
        lens.Files(files.Concat(ScopeJson.Items(code?["data"], "files")));
        lens.Files(ScopeJson.Items(code, "tests"), "test");
        lens.Keyed(ScopeJson.Items(code?["data"], "things"), "key", "product");
        foreach (var side in new[] { "callers", "callees" })
        {
            lens.SymbolItems(code?[side]);
        }

        lens.Symbols(band?["symbols"]);
        lens.Source(MapSource(lenses));
        lens.Source(code is null ? NotConfigured("code") : null);
        lens.Sources(Sources(code));
        lens.Edges(ScopeJson.Items(code, "edges").Concat(ScopeJson.Items(lenses, "edges")).Concat(Implements(files)));
        return lens;
    }

    /// <summary>The map feeds every lens as its own source; the code index feeds only the code band's symbols, which the code lens records.</summary>
    private static bool IsMapOrCodeIndex(string? id) =>
        string.Equals(id, "map", StringComparison.Ordinal) || string.Equals(id, "code-index", StringComparison.Ordinal);

    private static JsonObject NotConfigured(string id) => new() { ["id"] = id, ["state"] = "not-configured", ["reason"] = "not-configured" };

    private static IEnumerable<JsonNode> Implements(IEnumerable<JsonNode> files) =>
        files.SelectMany(file => ScopeJson.Strings(file, "implements").Select(to => (JsonNode)new JsonObject
        {
            ["from"] = "code:" + ScopeJson.Str(file, "path"),
            ["to"] = to,
            ["type"] = "implements",
            ["provenance"] = ScopeJson.Str(file, "provenance") ?? "deterministic",
            ["confirmed"] = ScopeJson.Bool(file, "confirmed") ?? false,
        }));

    private static LensBuilder HistoryLens(JsonNode? history, IReadOnlyList<string> anchors)
    {
        var lens = new LensBuilder("history", anchors);
        var incidents = history?["incidents"];
        lens.Records(ScopeJson.Items(history, "records"), "record", "key", "label", altLabelField: null);
        lens.Records(ScopeJson.Items(history, "decisions"), "decision", "node", "label", "text");
        lens.Records(ScopeJson.Items(history, "changes"), "commit", "key", "subject", altLabelField: null);
        lens.Records(ScopeJson.Items(incidents, "items"), "incident", "key", "label", "title");
        lens.Source(history is null ? NotConfigured("history") : null);
        lens.Sources(Sources(history));
        lens.Source(incidents is not null && !string.Equals(ScopeJson.Str(incidents, "state"), "ok", StringComparison.Ordinal) ? Source("incidents", incidents) : null);
        lens.Edges(ScopeJson.Items(history, "edges"));
        return lens;
    }

    private static JsonObject MapSource(JsonNode? lenses) =>
        Sources(lenses).FirstOrDefault(static s => string.Equals(ScopeJson.Str(s, "id"), "map", StringComparison.Ordinal)) ?? new JsonObject { ["id"] = "map", ["state"] = "ok" };

    /// <summary>A view's searched sources, and each name in its degraded list as a degraded source.</summary>
    private static IEnumerable<JsonObject> Sources(JsonNode? view)
    {
        foreach (var source in ScopeJson.Items(view?["searched"], "sources"))
        {
            yield return Source(ScopeJson.Str(source, "id") ?? "source", source);
        }

        foreach (var name in ScopeJson.Strings(view, "degraded"))
        {
            yield return new JsonObject { ["id"] = name, ["state"] = "degraded" };
        }
    }

    /// <summary>A coverage source: id, state, and the reason (spelled with hyphens, as the Recorded limits table has it), code, sync pair and catching-up commits when the view had them.</summary>
    internal static JsonObject Source(string id, JsonNode? from)
    {
        var source = new JsonObject { ["id"] = id, ["state"] = ScopeJson.Str(from, "state") ?? "ok" };
        if (ScopeJson.Str(from, "reason") is { Length: > 0 } reason)
        {
            source["reason"] = reason.Trim().ToLowerInvariant().Replace(' ', '-');
        }

        if (ScopeJson.Str(from, "code") is { Length: > 0 } code)
        {
            source["code"] = code;
        }

        if (ScopeJson.Obj(from, "catchingUp") is { } catching)
        {
            source["catchingUp"] = catching.DeepClone();
        }

        if (ScopeJson.Obj(from, "sync") is { } sync)
        {
            source["sync"] = sync.DeepClone();
        }

        return source;
    }
}
