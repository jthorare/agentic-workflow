using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

/// <summary>
/// A workflow.plan instance's facts as the files the stop view reads (line spec LN-API2). fact:plan is tasks.json (PKT's task plan), fact:holdoutSets holdout.json, fact:freezeRefused the
/// banner and fact:lensRounds the lens log. fact:design is schemas/line/design.json: sections, coverage, decisions, signals and contracts as arrays, read into the design packet's
/// files of the same names. The spec, the acceptance list, the escapes and the run records are not in fact:design and no instance fact holds them yet, so those sections are 'not yet' on the route.
/// </summary>
internal static class DesignStopFacts
{
    /// <summary>The instance's facts (JSON text by key without the 'fact:' prefix) as files.</summary>
    /// <param name="facts">The facts.</param>
    internal static Dictionary<string, string> Files(IReadOnlyDictionary<string, string> facts)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (fact, file) in new[] { ("plan", "tasks.json"), ("holdoutSets", "holdout.json"), ("freezeRefused", "freezeRefused.json") })
        {
            if (facts.TryGetValue(fact, out var text))
            {
                files[file] = text;
            }
        }

        if (facts.TryGetValue("lensRounds", out var rounds) && Parse(rounds) is JsonArray log)
        {
            files["design/lens-rounds.jsonl"] = new StringBuilder().AppendJoin('\n', log.Select(static r => r?.ToJsonString())).ToString();
        }

        if (facts.TryGetValue("design", out var design) && Parse(design) is JsonObject parts)
        {
            foreach (var (name, content) in DesignFiles(parts))
            {
                files["design/" + name] = content;
            }
        }

        return files;
    }

    private static Dictionary<string, string> DesignFiles(JsonObject parts)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        Put(files, "sections.json", parts["sections"], Sections);
        Put(files, "coverage.json", parts["coverage"], Coverage);
        Put(files, "decisions.json", parts["decisions"], static rows => new JsonObject { ["decisions"] = rows });
        Put(files, "signals.json", parts["signals"], Signals);
        Put(files, "contracts.json", parts["contracts"], Contracts);
        return files;
    }

    private static void Put(Dictionary<string, string> files, string name, JsonNode? part, Func<JsonArray, JsonObject> file)
    {
        if (part is JsonArray rows)
        {
            files[name] = file((JsonArray)rows.DeepClone()).ToJsonString();
        }
    }

    // A section's title is its heading and its single 'assumes' option a one-entry list.
    private static JsonObject Sections(JsonArray rows)
    {
        var sections = new JsonArray();
        foreach (var row in rows.OfType<JsonObject>())
        {
            var assumes = LineJson.Str(row, "assumes");
            LineJson.Append(sections, new JsonObject
            {
                ["id"] = LineJson.Str(row, "id"),
                ["heading"] = LineJson.Str(row, "title"),
                ["covers"] = row["covers"]?.DeepClone() ?? new JsonArray(),
                ["cites"] = row["cites"]?.DeepClone() ?? new JsonArray(),
                ["assumes"] = assumes is null ? [] : new JsonArray(assumes),
            });
        }

        return new JsonObject { ["sections"] = sections };
    }

    // The surface column is both the scope id and the key; a row with a reason is excluded.
    private static JsonObject Coverage(JsonArray rows)
    {
        var covered = new JsonArray();
        var excluded = new JsonArray();
        foreach (var row in rows.OfType<JsonObject>())
        {
            var surface = LineJson.Str(row, "surface");
            if (LineJson.Str(row, "excluded") is { } reason)
            {
                LineJson.Append(excluded, new JsonObject { ["key"] = surface, ["id"] = surface, ["reason"] = reason });
            }
            else
            {
                LineJson.Append(covered, new JsonObject { ["key"] = surface, ["id"] = surface, ["sections"] = row["sections"]?.DeepClone() ?? new JsonArray() });
            }
        }

        return new JsonObject { ["surfaces"] = covered, ["excluded"] = excluded };
    }

    // Item to signals becomes the file's map; a signal is named by its item and place in the list.
    private static JsonObject Signals(JsonArray rows)
    {
        var signals = new JsonObject();
        foreach (var row in rows.OfType<JsonObject>())
        {
            var item = LineJson.Str(row, "item") ?? string.Empty;
            var list = new JsonArray();
            var n = 0;
            foreach (var signal in LineJson.Items(row, "signals"))
            {
                n++;
                LineJson.Append(list, new JsonObject { ["id"] = item + "-s" + n.ToString(System.Globalization.CultureInfo.InvariantCulture), ["kind"] = LineJson.Str(signal, "kind"), ["statement"] = LineJson.Str(signal, "statement"), ["runsAt"] = LineJson.Str(signal, "runs") });
            }

            signals[item] = list;
        }

        return new JsonObject { ["signals"] = signals };
    }

    // An edit is named X<n> in the order written; its 'edit' text is the change.
    private static JsonObject Contracts(JsonArray rows)
    {
        var edits = new JsonArray();
        var n = 0;
        foreach (var row in rows.OfType<JsonObject>())
        {
            n++;
            LineJson.Append(edits, new JsonObject
            {
                ["id"] = "X" + n.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["lane"] = LineJson.Str(row, "lane"),
                ["target"] = new JsonObject { ["file"] = LineJson.Str(row, "file"), ["heading"] = LineJson.Str(row, "heading") },
                ["quote"] = LineJson.Str(row, "quote"),
                ["change"] = LineJson.Str(row, "edit"),
                ["row"] = LineJson.Number(row, "row"),
            });
        }

        return new JsonObject { ["edits"] = edits };
    }

    private static JsonNode? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;   // boundary: a fact is text a Step or a person wrote; one that is not JSON holds nothing
        }
    }
}
