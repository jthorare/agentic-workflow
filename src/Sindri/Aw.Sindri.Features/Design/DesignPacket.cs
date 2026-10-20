using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

/// <summary>
/// The design packet assembler (line spec §3.2 row 2, §4.4): the thirteen files of the candidate packet, built from the writer's files, the lens log and the scope packet.
/// What code owns is stamped by code: the acceptance file's parent and statuses, trust, the escapes, packet.json's links and index.json's chain. Every other file passes through
/// as written; a file the writer left out stays out, so aw-packets' validation names it.
/// </summary>
public static class DesignPacket
{
    private const int SchemaVersion = 1;

    private static readonly string[] _passThrough =
    [
        "spec.md", "sections.json", "coverage.json", "decisions.json", "acceptance.json", "signals.json", "contracts.json", "lens-rounds.jsonl", "style.json", "escapes.json", "runs.json",
    ];

    /// <summary>The packet's files, in the order the packet kind lists them.</summary>
    /// <param name="facts">The design files as the writer left them, the lens log, the run records and the style pins, with the scope packet's files under 'scope/'.</param>
    /// <param name="identity">The packet's id, repository and base commit.</param>
    /// <param name="scopeHash">The frozen scope packet's hash: the design's one parent.</param>
    /// <returns>The files.</returns>
    public static IReadOnlyList<LineFile> Build(LineFacts facts, PacketIdentity identity, string scopeHash)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(scopeHash);
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in _passThrough.Where(facts.Has))
        {
            files[name] = LineJson.Terminated(facts.Text(name));
        }

        if (facts.Json("acceptance.json") is { } acceptance)
        {
            files["acceptance.json"] = LineJson.Rewritten(files["acceptance.json"], Stamped(acceptance, facts, LineJson.Hash(facts.Text("scope/acceptance.json"))));
        }

        if (facts.Has("acceptance.json") && facts.Json("scope/acceptance.json") is not null)
        {
            files["escapes.json"] = Escapes(facts, files.GetValueOrDefault("escapes.json"));
        }

        var all = new List<LineFile> { new("packet.json", LineJson.Write(Packet(identity))), new("index.json", LineJson.Write(Index(facts, files, scopeHash))) };
        all.AddRange(_passThrough.Where(files.ContainsKey).Select(name => new LineFile(name, files[name])));
        return all;
    }

    private static JsonObject Packet(PacketIdentity identity) => new()
    {
        ["schemaVersion"] = SchemaVersion,
        ["kind"] = "design",
        ["id"] = identity.Packet,
        ["repo"] = identity.Repo,
        ["baseSha"] = identity.BaseSha,
        ["status"] = "draft",
        ["hash"] = null,
        ["links"] = new JsonObject
        {
            ["spec"] = "spec.md",
            ["sections"] = "sections.json",
            ["coverage"] = "coverage.json",
            ["decisions"] = "decisions.json",
            ["acceptance"] = "acceptance.json",
            ["signals"] = "signals.json",
            ["contracts"] = "contracts.json",
            ["lensRounds"] = "lens-rounds.jsonl",
            ["style"] = "style.json",
            ["escapes"] = "escapes.json",
            ["runs"] = "runs.json",
            ["index"] = "index.json",
        },
    };

    private static JsonObject Index(LineFacts facts, Dictionary<string, string> files, string scopeHash) => new()
    {
        ["schemaVersion"] = SchemaVersion,
        ["kind"] = "design",
        ["parents"] = new JsonArray(scopeHash),
        ["acceptance"] = new JsonObject
        {
            ["self"] = files.TryGetValue("acceptance.json", out var own) ? LineJson.Hash(own) : null,
            ["extends"] = facts.Has("scope/acceptance.json") ? LineJson.Hash(facts.Text("scope/acceptance.json")) : null,
        },
    };

    // The acceptance file with its parent set to the scope acceptance's hash and every item failing with no evidence (only the proof pack flips one); trust carries from the scope, and
    // an item the design added is untrusted until stop 2's approve (LC-6).
    private static JsonObject Stamped(JsonNode acceptance, LineFacts facts, string scopeAcceptanceHash)
    {
        var scopeTrust = LineJson.Items(facts.Json("scope/acceptance.json"), "items").ToDictionary(static i => LineJson.Str(i, "id") ?? string.Empty, static i => LineJson.Str(i, "trust") ?? "untrusted", StringComparer.Ordinal);
        var stamped = new JsonObject { ["schemaVersion"] = SchemaVersion, ["parent"] = facts.Has("scope/acceptance.json") ? scopeAcceptanceHash : null };
        var items = new JsonArray();
        foreach (var item in LineJson.Items(acceptance, "items"))
        {
            var copy = (JsonObject)item.DeepClone();
            copy["trust"] = scopeTrust.GetValueOrDefault(LineJson.Str(item, "id") ?? string.Empty, "untrusted");
            if (!string.Equals(LineJson.Str(item, "status"), "superseded", StringComparison.Ordinal))
            {
                copy["status"] = "failing";
            }

            copy["evidence"] = new JsonArray();
            LineJson.Append(items, copy);
        }

        stamped["items"] = items;
        foreach (var (name, value) in ((JsonObject)acceptance).Where(static p => p.Key is not ("schemaVersion" or "parent" or "items")))
        {
            stamped[name] = value?.DeepClone();
        }

        return stamped;
    }

    // The escapes the writer listed, plus one for each item and surface the design adds that the scope packet lacks (LC-7).
    private static string Escapes(LineFacts facts, string? written)
    {
        var file = written is not null && JsonNode.Parse(written) is JsonObject kept ? kept : new JsonObject { ["schemaVersion"] = SchemaVersion, ["escapes"] = new JsonArray() };
        var escapes = file["escapes"] as JsonArray ?? [];
        file["escapes"] = escapes;
        foreach (var item in LineJson.Items(facts.Json("acceptance.json"), "items"))
        {
            AddMissing(facts, escapes, item);
        }

        return written is not null ? LineJson.Rewritten(written, file) : LineJson.Write(file);
    }

    private static void AddMissing(LineFacts facts, JsonArray escapes, JsonNode item)
    {
        var id = LineJson.Str(item, "id") ?? string.Empty;
        bool Listed(string kind, string target) => escapes.Any(e => string.Equals(LineJson.Str(e, "kind"), kind, StringComparison.Ordinal) && string.Equals(LineJson.Str(e, "id"), target, StringComparison.Ordinal));
        var inScope = LineJson.Items(facts.Json("scope/acceptance.json"), "items").Any(i => string.Equals(LineJson.Str(i, "id"), id, StringComparison.Ordinal));
        if (!inScope && !Listed("acceptance", id))
        {
            LineJson.Append(escapes, Escape("acceptance", id, "Added by the design; the scope packet does not have it."));
        }

        var scopeSurfaces = LineJson.Items(facts.Json("scope/scope-map.json"), "surfaces").Select(static s => LineJson.Str(s, "key")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var surface in LineJson.Strings(item, "surfaces").Where(s => !scopeSurfaces.Contains(s) && !Listed("surface", s)))
        {
            LineJson.Append(escapes, Escape("surface", surface, $"Used by {id}; the scope map does not list it."));
        }
    }

    private static JsonObject Escape(string kind, string id, string why) => new() { ["from"] = "scoping", ["station"] = "design", ["kind"] = kind, ["id"] = id, ["why"] = why };
}
