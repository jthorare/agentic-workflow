using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.Scoping;

/// <summary>The gap log's rule (spec 4.6), pure: pairs the searcher's candidates with the judge's rulings and appends the round.</summary>
internal static class GapLog
{
    /// <summary>The rule id of a confirmed gap.</summary>
    internal const string Rule = "SCP-GAP-001";

    private const string RealGap = "real-gap";
    private const string AlreadyCovered = "already-covered";
    private const string NotInScope = "not-in-scope";
    private const string Uncertain = "uncertain";

    private static readonly string[] _rulings = [RealGap, AlreadyCovered, NotInScope, Uncertain];

    /// <summary>Appends a round.</summary>
    /// <param name="gapSearch">The cumulative record so far.</param>
    /// <param name="candidates">The searcher's fact.</param>
    /// <param name="rulings">The judge's fact.</param>
    /// <param name="findings">The findings fact so far.</param>
    /// <returns>The round, or the reason the Step fails.</returns>
    internal static Result<GapLogResult> Apply(JsonNode? gapSearch, JsonNode? candidates, JsonNode? rulings, JsonNode? findings)
    {
        var paired = Pair([.. ScopeJson.Items(candidates, "candidates")], [.. ScopeJson.Items(rulings, "rulings")]);
        if (paired.IsError)
        {
            return paired.Error;
        }

        var record = gapSearch is JsonObject held ? (JsonObject)held.DeepClone() : [];
        var rounds = ScopeJson.Arr(record, "rounds") ?? [];
        record["rounds"] = rounds;
        var round = rounds.Count + 1;
        var entries = paired.Value.Select((p, i) => Entry(p.Candidate, p.Ruling, round, i + 1)).ToList();
        var confirmed = entries.Where(static e => string.Equals(ScopeJson.Str(e, "ruling"), RealGap, StringComparison.Ordinal)).ToList();
        ScopeJson.Append(rounds, Round(round, candidates, rulings, entries));
        record["counts"] = Totals(rounds);
        var anchors = ScopeJson.ToArray(confirmed.Select(static e => ScopeJson.Str(e, "anchor")).OfType<string>().Where(static a => a.Trim().Length > 0).Distinct(StringComparer.Ordinal));
        return confirmed.Count == 0
            ? new GapLogResult("clear", record, anchors, Findings: null, new JsonObject { ["round"] = round })
            : new GapLogResult("confirmed", record, anchors, WithFindings(findings, confirmed), Closed: null);
    }

    // Each candidate with its ruling, in the searcher's order; the reason the Step fails when an id is given twice, a ruling names no candidate, a candidate has none or the word is not one the judge gives.
    private static Result<List<(JsonNode Candidate, JsonNode Ruling)>> Pair(List<JsonNode> candidates, List<JsonNode> rulings)
    {
        var byId = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (ScopeJson.Str(candidate, "id") is not { Length: > 0 } id || !byId.TryAdd(id, candidate))
            {
                return SindriErrors.InvalidInput.With($"gap-log: the candidate id '{ScopeJson.Str(candidate, "id") ?? "none"}' is missing or given twice (fix: the searcher numbers its candidates C1, C2 and so on, once each)");
            }
        }

        var ruled = Rulings(byId, rulings);
        if (ruled.IsError)
        {
            return ruled.Error;
        }

        if (byId.Keys.FirstOrDefault(id => !ruled.Value.ContainsKey(id)) is { } unruled)
        {
            return SindriErrors.InvalidInput.With($"gap-log: the candidate '{unruled}' has no ruling (fix: run the judge again over this round's candidates)");
        }

        return candidates.Select(c => (c, ruled.Value[ScopeJson.Str(c, "id")!])).ToList();   // why: every id was checked present above
    }

    private static Result<Dictionary<string, JsonNode>> Rulings(Dictionary<string, JsonNode> candidates, List<JsonNode> rulings)
    {
        var ruled = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var ruling in rulings)
        {
            var id = ScopeJson.Str(ruling, "id") ?? "none";
            var word = ScopeJson.Str(ruling, "ruling");
            if (!candidates.ContainsKey(id))
            {
                return SindriErrors.InvalidInput.With($"gap-log: the judge ruled on '{id}', which is not one of the searcher's candidates (fix: run the judge again over this round's candidates)");
            }

            if (word is null || !_rulings.Contains(word, StringComparer.Ordinal))
            {
                return SindriErrors.InvalidInput.With($"gap-log: the ruling '{word ?? "none"}' on '{id}' is not one of {string.Join(", ", _rulings)}");
            }

            if (!ruled.TryAdd(id, ruling))
            {
                return SindriErrors.InvalidInput.With($"gap-log: the judge ruled on '{id}' twice (fix: run the judge again over this round's candidates)");
            }
        }

        return ruled;
    }

    // The candidate as the record keeps it: its own fields (the argument stays, for the owner's reading), a global id and the ruling.
    private static JsonObject Entry(JsonNode candidate, JsonNode ruling, int round, int position) => new()
    {
        ["id"] = string.Create(CultureInfo.InvariantCulture, $"G{round}.{position}"),
        ["title"] = ScopeJson.Str(candidate, "title"),
        ["kind"] = ScopeJson.Str(candidate, "kind"),
        ["citation"] = ScopeJson.Str(candidate, "citation"),
        ["excerpt"] = ScopeJson.Str(candidate, "excerpt"),
        ["query"] = ScopeJson.Str(candidate, "query"),
        ["anchor"] = ScopeJson.Str(candidate, "anchor"),
        ["why"] = ScopeJson.Str(candidate, "why"),
        ["ruling"] = ScopeJson.Str(ruling, "ruling"),
        ["confidence"] = ruling is JsonObject r ? r["confidence"]?.DeepClone() : null,
        ["provider"] = ScopeJson.Str(ruling, "provider"),
        ["decisionId"] = ScopeJson.Str(ruling, "decisionId"),
    };

    private static JsonObject Round(int round, JsonNode? candidates, JsonNode? rulings, List<JsonObject> entries)
    {
        int Count(string word) => entries.Count(e => string.Equals(ScopeJson.Str(e, "ruling"), word, StringComparison.Ordinal));
        return new JsonObject
        {
            ["round"] = round,
            ["agent"] = new JsonObject { ["reportedModel"] = ScopeJson.Str(candidates, "model") },
            ["judge"] = new JsonObject { ["providers"] = (ScopeJson.Arr(ScopeJson.Obj(rulings, "judge"), "providers") ?? []).DeepClone() },
            ["searches"] = (ScopeJson.Arr(candidates, "searches") ?? []).DeepClone(),
            ["candidates"] = new JsonArray([.. entries.Cast<JsonNode>()]),
            ["counts"] = new JsonObject
            {
                ["found"] = entries.Count,
                ["confirmed"] = Count(RealGap),
                ["alreadyCovered"] = Count(AlreadyCovered),
                ["notInScope"] = Count(NotInScope),
                ["uncertain"] = Count(Uncertain),
            },
        };
    }

    private static JsonObject Totals(JsonArray rounds) => new()
    {
        ["rounds"] = rounds.Count,
        ["found"] = rounds.Sum(static r => ScopeJson.Int(ScopeJson.Obj(r, "counts"), "found") ?? 0),
        ["confirmed"] = rounds.Sum(static r => ScopeJson.Int(ScopeJson.Obj(r, "counts"), "confirmed") ?? 0),
    };

    // The findings so far, without an earlier round's gap findings (they are stale once the map is regathered), and one for each gap this round confirmed.
    private static JsonArray WithFindings(JsonNode? held, List<JsonObject> confirmed)
    {
        var findings = new JsonArray();
        foreach (var finding in ScopeJson.Items(held).Where(static f => !string.Equals(ScopeJson.Str(f, "rule"), Rule, StringComparison.Ordinal)))
        {
            ScopeJson.Append(findings, finding.DeepClone());
        }

        foreach (var gap in confirmed)
        {
            var title = ScopeJson.Str(gap, "title");
            ScopeJson.Append(findings, new JsonObject
            {
                ["rule"] = Rule,
                ["item"] = title,
                ["message"] = $"the gap search confirmed a gap the map misses: {title} ({ScopeJson.Str(gap, "citation")})",
                ["fix"] = "Add it to the map, or exclude it with a reason, then check again.",
                ["severity"] = "fail",
            });
        }

        return findings;
    }
}
