using System.Text;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Packets;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// The scope packet assembler (scoping spec §8.3): the fourteen files aw-packets' scoping kind lists, built from the facts the evidence step
/// holds, with trust stamped by code. Every JSON file carries schemaVersion 1; the lens file is built from fact:lenses with every node, edge
/// and item 'untrusted' (C14's answer provenance has no field to ride on yet).
/// </summary>
internal static class ScopePacket
{
    private const int SchemaVersion = 1;

    /// <summary>The packet's files, paths relative to the run area ('scope-packet/...').</summary>
    /// <param name="facts">The evidence step's facts after uncited items were flagged.</param>
    internal static IReadOnlyList<ScopingDeskFile> Build(ScopeFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        List<ScopingDeskFile> files =
        [
            Json("scope-map.json", Versioned(StampedMap(facts))),
            Json("acceptance.json", Versioned(StampedAcceptance(facts))),
            Json("coverage.json", Versioned(Plain(facts.ObjectCopy("coverage")))),
            Json("lenses.json", Lenses(facts["lenses"])),
            Json("relevance.json", Versioned(Plain(facts.ObjectCopy("relevance")))),
            Json("size.json", Versioned(Plain(facts.ObjectCopy("decision")))),
            Json("questions.json", new JsonObject { ["schemaVersion"] = SchemaVersion, ["questions"] = Plain(facts["questions"]?.DeepClone() ?? new JsonArray()) }),
            Lines("answers.jsonl", ScopeJson.Items(facts["answerLog"]).Select(static e => Plain(e.DeepClone()))),
            Json("challenge.json", Versioned(Plain(facts.ObjectCopy("challenge")))),
            Json("gap-search.json", Versioned(Plain(facts.ObjectCopy("gapSearch")))),
            Lines("rounds.jsonl", ScopeJson.Items(facts["rounds"]).Select(static e => Plain(e.DeepClone()))),
            Json("runs.json", new JsonObject { ["schemaVersion"] = SchemaVersion, ["runs"] = Plain(Runs(facts).DeepClone()) }),
            Json("sources.json", new JsonObject { ["schemaVersion"] = SchemaVersion, ["sources"] = Sources(facts["brief"]) }),
            new ScopingDeskFile($"{ScopeEvidence.PacketDirectory}/brief.md", Brief(facts["brief"]), Append: false),
        ];
        return files;
    }

    /// <summary>
    /// The node without any 'trust' key, at every depth. Trust is stamped by code (SCP-TRUST-001), so a label a model wrote into the facts, 'owner' above all,
    /// never reaches a packet file, where the session fence (PacketFileFencing in Host) would read it as an owner's decision.
    /// </summary>
    private static T Plain<T>(T node)
        where T : JsonNode
    {
        if (node is JsonObject obj)
        {
            _ = obj.Remove("trust");
        }

        foreach (var child in ((node as JsonObject)?.Select(static p => p.Value).ToList() ?? (node as JsonArray)?.ToList() ?? []).OfType<JsonNode>())
        {
            _ = Plain(child);
        }

        return node;
    }

    private static JsonObject Versioned(JsonObject body)
    {
        var file = new JsonObject { ["schemaVersion"] = SchemaVersion };
        foreach (var (name, value) in body.ToList())
        {
            _ = body.Remove(name);
            if (!string.Equals(name, "schemaVersion", StringComparison.Ordinal))
            {
                file[name] = value;
            }
        }

        return file;
    }

    private static JsonObject StampedMap(ScopeFacts facts)
    {
        var map = Plain(facts.ObjectCopy("scopeMap"));
        Envelope(map, facts);
        foreach (var surface in ScopeJson.Items(map, "surfaces").OfType<JsonObject>())
        {
            surface["trust"] = ScopeTrust.For($"{ScopeJson.Str(surface, "title")} {ScopeJson.Str(surface, "detail")}", [.. ScopeJson.Strings(surface, "citations")], facts);
        }

        foreach (var implication in ScopeJson.Items(map, "implications").OfType<JsonObject>())
        {
            implication["trust"] = ScopeTrust.For(ScopeJson.Str(implication, "text") ?? string.Empty, [.. ScopeJson.Strings(implication, "citations")], facts);
        }

        foreach (var path in ScopeJson.Items(map, "unmapped").OfType<JsonObject>())
        {
            path["trust"] = ScopeTrust.Untrusted;
        }

        return map;
    }

    // The envelope the line reads (LineScope): the commit the scoping read (the evidence Step's 'baseSha' fact, never the model's) and the change's slug, the design packet's id.
    private static void Envelope(JsonObject map, ScopeFacts facts)
    {
        _ = map.Remove("baseSha");
        _ = map.Remove("change");
        if (facts["baseSha"] is JsonValue sha && sha.TryGetValue<string>(out var baseSha) && !string.IsNullOrWhiteSpace(baseSha))
        {
            map["baseSha"] = baseSha;
        }

        if (ScopeJson.Str(facts["brief"], "subject") is { } subject && !string.IsNullOrWhiteSpace(subject))
        {
            map["change"] = PacketSlugs.Slug(subject);
        }
    }

    private static JsonObject StampedAcceptance(ScopeFacts facts)
    {
        var acceptance = Plain(facts.ObjectCopy("acceptance"));
        acceptance["parent"] = null;
        foreach (var item in ScopeJson.Items(acceptance, "items").OfType<JsonObject>())
        {
            item["trust"] = ScopeTrust.For(ScopeJson.Str(item, "text") ?? string.Empty, [.. ScopeJson.Strings(item, "citations")], facts);
            item["status"] = "failing";
            item["evidence"] = new JsonArray();
            item["parent"] = null;
        }

        return acceptance;
    }

    /// <summary>lenses.json: the four entries with every field, and trust 'untrusted' on every node, edge and item.</summary>
    internal static JsonObject Lenses(JsonNode? lenses)
    {
        var file = new JsonObject { ["schemaVersion"] = SchemaVersion };
        foreach (var lens in ScopeRules.Lenses)
        {
            var entry = ScopeJson.Obj(lenses, lens);
            file[lens] = new JsonObject
            {
                ["anchors"] = ScopeJson.ToArray(ScopeJson.Strings(entry, "anchors")),
                ["depth"] = ScopeJson.Int(entry, "depth") ?? 0,
                ["hops"] = ScopeJson.ToArray(ScopeJson.Strings(entry, "hops")),
                ["mapVersion"] = ScopeJson.Str(entry, "mapVersion") ?? string.Empty,
                ["foundCount"] = ScopeJson.Int(entry, "foundCount") ?? 0,
                ["nodes"] = Untrusted(ScopeJson.Items(entry, "nodes")),
                ["edges"] = Untrusted(ScopeJson.Items(entry, "edges")),
                ["items"] = Untrusted(ScopeJson.Items(entry, "items")),
                ["truncated"] = ScopeJson.ToArray(ScopeJson.Strings(entry, "truncated")),
            };
        }

        return file;
    }

    private static JsonArray Untrusted(IEnumerable<JsonNode> entries)
    {
        var stamped = new JsonArray();
        foreach (var entry in entries)
        {
            var copy = Plain((JsonObject)entry.DeepClone());
            copy["trust"] = ScopeTrust.Untrusted;
            ScopeJson.Append(stamped, copy);
        }

        return stamped;
    }

    private static JsonArray Runs(ScopeFacts facts) => facts["runs"] is JsonArray all ? all : ScopeJson.Arr(facts["runs"], "runs") ?? [];

    private static JsonArray Sources(JsonNode? brief)
    {
        var sources = new JsonArray();
        foreach (var reference in ScopeJson.Items(brief, "refs"))
        {
            ScopeJson.Append(sources, new JsonObject
            {
                ["ref"] = ScopeJson.Str(reference, "key") ?? ScopeJson.Str(reference, "id"),
                ["trust"] = ScopeJson.Str(reference, "trust") ?? ScopeTrust.Untrusted,
                ["version"] = ScopeJson.Str(reference, "version") ?? string.Empty,
                ["fetchedAt"] = ScopeJson.Str(reference, "fetchedAt") ?? string.Empty,
                ["truncated"] = false,
                ["denied"] = false,
            });
        }

        return sources;
    }

    /// <summary>brief.md: the brief's text, fenced where it came from an untrusted source (the fence is longer than any run of backticks inside).</summary>
    private static string Brief(JsonNode? brief)
    {
        var text = ScopeJson.Str(brief, "text") ?? string.Empty;
        var untrusted = ScopeJson.Items(brief, "refs").Any(static r => !string.Equals(ScopeJson.Str(r, "trust"), ScopeTrust.Owner, StringComparison.Ordinal));
        if (!untrusted)
        {
            return text.EndsWith('\n') ? text : text + "\n";
        }

        var fence = "```";
        while (text.Contains(fence, StringComparison.Ordinal))
        {
            fence += "`";
        }

        return $"{fence}untrusted\n{text.TrimEnd('\n')}\n{fence}\n";
    }

    private static ScopingDeskFile Json(string name, JsonNode body) => new($"{ScopeEvidence.PacketDirectory}/{name}", ScopeJson.Write(body, indented: true) + "\n", Append: false);

    private static ScopingDeskFile Lines(string name, IEnumerable<JsonNode> entries)
    {
        var text = new StringBuilder();
        foreach (var entry in entries)
        {
            _ = text.Append(ScopeJson.Write(entry)).Append('\n');
        }

        return new ScopingDeskFile($"{ScopeEvidence.PacketDirectory}/{name}", text.ToString(), Append: false);
    }
}
