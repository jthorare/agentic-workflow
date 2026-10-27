using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// The relevance decision table and the fact rewriting it drives (spec 4.3): pure, no I/O. An entry leaves the lenses and the coverage only when the agent and the judge both say it is not 'in'
/// and the agent's citation resolves. Every other case keeps it in, and a case where the two did not settle it (a disagreement, an uncertain ruling, a missing label, an uncited removal) is flagged as disputed.
/// </summary>
internal static class RelevanceApply
{
    private const string Fix = "Fix: run the relevance review again on the current lenses.";

    /// <summary>Decides every entry of the lenses from the agent's verdicts and the judge's rulings and rewrites the facts.</summary>
    /// <param name="lenses">The lenses fact.</param>
    /// <param name="coverage">The coverage fact.</param>
    /// <param name="brief">The brief fact, for the refs a citation may name.</param>
    /// <param name="verdicts">The agent's verdicts fact.</param>
    /// <param name="rulings">The judge's rulings fact.</param>
    /// <returns>The facts to write, or the error that fails the Step: a verdict or ruling for an id the lenses do not hold, or an id twice.</returns>
    internal static Result<RelevanceApplied> Apply(JsonNode lenses, JsonNode coverage, JsonNode brief, JsonNode verdicts, JsonNode rulings)
    {
        ArgumentNullException.ThrowIfNull(lenses);
        ArgumentNullException.ThrowIfNull(coverage);
        ArgumentNullException.ThrowIfNull(brief);
        ArgumentNullException.ThrowIfNull(verdicts);
        ArgumentNullException.ThrowIfNull(rulings);
        var entries = RelevanceEntries.Of(lenses);
        var known = entries.Select(static e => e.Id).ToHashSet(StringComparer.Ordinal);
        var agent = ById("verdicts", ScopeJson.Items(verdicts, "verdicts"), known, ["in", "out", "noise"]);
        if (agent.IsError)
        {
            return agent.Error;
        }

        var judge = ById("rulings", ScopeJson.Items(rulings, "rulings"), known, ["in", "out", "noise", "uncertain"]);
        if (judge.IsError)
        {
            return judge.Error;
        }

        var citable = Citable(lenses, brief, known);
        var decisions = entries.Select(e => Decide(e, agent.Value.GetValueOrDefault(e.Id), judge.Value.GetValueOrDefault(e.Id), citable)).ToList();
        var removed = decisions.Where(static d => d.Result != "in").ToList();
        var newLenses = Filtered(lenses, removed);
        return new RelevanceApplied(newLenses, Recovered(coverage, newLenses), Relevance(lenses, verdicts, rulings, decisions), MapFixes(removed));
    }

    private sealed record Decision(RelevanceEntry Entry, JsonNode? Verdict, JsonNode? Ruling, string Result, bool Disputed);

    private static Decision Decide(RelevanceEntry entry, JsonNode? verdict, JsonNode? ruling, HashSet<string> citable)
    {
        var agent = ScopeJson.Str(verdict, "class");
        var judge = ScopeJson.Str(ruling, "class");
        var cited = ScopeJson.Str(verdict, "citation") is { Length: > 0 } citation && citable.Contains(citation);
        var (result, disputed) = (agent, judge) switch
        {
            (null, _) or (_, null) or (_, "uncertain") => ("in", true),
            ("in", "in") => ("in", false),
            ("in", _) or (_, "in") => ("in", true),
            _ => cited ? (judge ?? "in", false) : ("in", true),
        };
        return new Decision(entry, verdict, ruling, result, disputed);
    }

    // The verdicts or rulings by id; an id the lenses do not hold, a row with no id, an id twice or a class outside the allowed ones fails the Step.
    private static Result<Dictionary<string, JsonNode>> ById(string what, IEnumerable<JsonNode> rows, HashSet<string> known, string[] classes)
    {
        var byId = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (ScopeJson.Str(row, "id") is not { } id)
            {
                return SindriErrors.InvalidInput.With($"relevance-apply: a row of the {what} has no id. {Fix}");
            }

            if (!known.Contains(id))
            {
                return SindriErrors.InvalidInput.With($"relevance-apply: the {what} name {id}, which the lenses do not hold. {Fix}");
            }

            if (ScopeJson.Str(row, "class") is not { } cls || !classes.Contains(cls, StringComparer.Ordinal))
            {
                return SindriErrors.InvalidInput.With($"relevance-apply: the {what} give {id} the class '{ScopeJson.Str(row, "class") ?? "none"}', not one of {string.Join(", ", classes)}. {Fix}");
            }

            if (!byId.TryAdd(id, row))
            {
                return SindriErrors.InvalidInput.With($"relevance-apply: the {what} name {id} twice. {Fix}");
            }
        }

        return byId;
    }

    // What a citation may name: a lens item id, a node key of the lenses or a ref of the brief.
    private static HashSet<string> Citable(JsonNode lenses, JsonNode brief, HashSet<string> ids)
    {
        var citable = new HashSet<string>(ids, StringComparer.Ordinal);
        foreach (var lens in ScopeRules.Lenses)
        {
            foreach (var node in ScopeJson.Items(ScopeJson.Obj(lenses, lens), "nodes"))
            {
                if (ScopeJson.Str(node, "key") is { } key)
                {
                    _ = citable.Add(key);
                }
            }
        }

        foreach (var reference in ScopeJson.Items(brief, "refs"))
        {
            if (ScopeJson.Str(reference, "id") is { } id)
            {
                _ = citable.Add(id);
            }
        }

        return citable;
    }

    private static JsonObject Filtered(JsonNode lenses, List<Decision> removed)
    {
        var gone = removed.Select(static d => d.Entry.Id).ToHashSet(StringComparer.Ordinal);
        var goneKeys = removed.Select(static d => d.Entry.Key).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var copy = lenses.DeepClone() as JsonObject ?? [];
        foreach (var lens in ScopeRules.Lenses)
        {
            if (ScopeJson.Obj(copy, lens) is not { } entry)
            {
                continue;
            }

            var before = RelevanceEntries.Of(copy).Count(e => e.Lens == lens);
            if (ScopeJson.Arr(entry, "nodes") is { } nodes)
            {
                entry["nodes"] = Keep(nodes, static n => ScopeJson.Str(n, "lensItem"), gone);
            }

            if (ScopeJson.Arr(entry, "items") is { } items)
            {
                entry["items"] = Keep(items, static n => ScopeJson.Str(n, "lensItem"), gone);
            }

            if (ScopeJson.Arr(entry, "edges") is { } edges)
            {
                entry["edges"] = Keep(edges, static e => ScopeJson.Str(e, "from"), goneKeys, static e => ScopeJson.Str(e, "to"));
            }

            var dropped = before - RelevanceEntries.Of(copy).Count(e => e.Lens == lens);
            if (ScopeJson.Int(entry, "foundCount") is { } found)
            {
                entry["foundCount"] = Math.Max(0, found - dropped);
            }
        }

        return copy;
    }

    // The elements whose key (and second key, for an edge) is not in the removed set, as clones.
    private static JsonArray Keep(JsonArray array, Func<JsonNode, string?> key, HashSet<string> removed, Func<JsonNode, string?>? other = null) =>
        new([.. array.OfType<JsonNode>().Where(n => !Gone(key(n), removed) && !(other is not null && Gone(other(n), removed))).Select(static n => n.DeepClone())]);

    private static bool Gone(string? key, HashSet<string> removed) => key is not null && removed.Contains(key);

    // The coverage rows follow the kept entries: the count is the lens's foundCount (SCP-LENS-001 holds the two equal), the ids are the kept ones.
    private static JsonObject Recovered(JsonNode coverage, JsonObject newLenses)
    {
        var copy = coverage.DeepClone() as JsonObject ?? [];
        foreach (var row in ScopeJson.Items(copy, "lenses").OfType<JsonObject>())
        {
            var lens = ScopeJson.Str(row, "lens");
            if (lens is null || ScopeJson.Obj(newLenses, lens) is not { } entry)
            {
                continue;
            }

            var ids = RelevanceEntries.Of(newLenses).Where(e => e.Lens == lens).Select(static e => e.Id).ToList();
            var count = ScopeJson.Int(entry, "foundCount") ?? ids.Count;
            if (!string.Equals(ScopeJson.Str(row, "state"), "not-applicable", StringComparison.Ordinal))
            {
                row["state"] = count > 0 ? "found" : ScopeJson.Items(row, "limits").Any() ? "not-searched" : "nothing-found";
            }

            var found = Own(row, "found");
            found["count"] = count;
            found["ids"] = ScopeJson.ToArray(ids);
            Own(row, "mapCoverage")["mapped"] = count;
        }

        return copy;
    }

    private static JsonObject Own(JsonObject parent, string name)
    {
        if (ScopeJson.Obj(parent, name) is { } existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[name] = created;
        return created;
    }

    private static JsonObject Relevance(JsonNode lenses, JsonNode verdicts, JsonNode rulings, List<Decision> decisions)
    {
        var entries = new JsonArray();
        foreach (var d in decisions)
        {
            ScopeJson.Append(entries, Entry(d));
        }

        var truncated = ScopeRules.Lenses.Where(l => ScopeJson.Items(ScopeJson.Obj(lenses, l), "truncated").Any());
        return new JsonObject
        {
            ["agent"] = new JsonObject { ["reportedModel"] = ScopeJson.Str(verdicts, "model") ?? "unknown" },
            ["judge"] = new JsonObject { ["providers"] = ScopeJson.ToArray(ScopeJson.Strings(ScopeJson.Obj(rulings, "judge"), "providers")) },
            ["counts"] = new JsonObject
            {
                ["reviewed"] = decisions.Count,
                ["in"] = decisions.Count(static d => d.Result == "in"),
                ["out"] = decisions.Count(static d => d.Result == "out"),
                ["noise"] = decisions.Count(static d => d.Result == "noise"),
                ["disputed"] = decisions.Count(static d => d.Disputed),
            },
            ["truncated"] = ScopeJson.ToArray(truncated),
            ["entries"] = entries,
        };
    }

    private static JsonObject Entry(Decision d)
    {
        var entry = new JsonObject { ["id"] = d.Entry.Id, ["lens"] = d.Entry.Lens };
        if (d.Entry.Key is { } key)
        {
            entry["key"] = key;
        }

        entry["kind"] = d.Entry.Kind;
        entry["label"] = d.Entry.Label;
        entry["agent"] = d.Verdict is null ? null : Copy(d.Verdict, "class", "reason", "citation", "fix", "relabelTo");
        entry["judge"] = d.Ruling is null ? null : Copy(d.Ruling, "class", "confidence", "provider", "decisionId", "reasonCode");
        entry["result"] = d.Result;
        entry["disputed"] = d.Disputed;
        return entry;
    }

    // The named fields of a verdict or ruling that are present, as clones (a JSON null stays null).
    private static JsonObject Copy(JsonNode source, params string[] fields)
    {
        var copy = new JsonObject();
        if (source is JsonObject obj)
        {
            foreach (var field in fields.Where(obj.ContainsKey))
            {
                copy[field] = obj[field]?.DeepClone();
            }
        }

        return copy;
    }

    private static JsonObject MapFixes(List<Decision> removed)
    {
        var fixes = new JsonArray();
        foreach (var d in removed.Where(static d => d.Entry.Key is not null))
        {
            var action = ScopeJson.Str(d.Verdict, "fix") ?? "ignore";
            ScopeJson.Append(fixes, new JsonObject
            {
                ["id"] = d.Entry.Id,
                ["node"] = d.Entry.Key,
                ["action"] = action,
                ["relabelTo"] = string.Equals(action, "relabel", StringComparison.Ordinal) ? ScopeJson.Str(d.Verdict, "relabelTo") : null,
                ["reason"] = ScopeJson.Str(d.Verdict, "reason"),
                ["citation"] = ScopeJson.Str(d.Verdict, "citation"),
                ["status"] = "proposed",
            });
        }

        return new JsonObject { ["fixes"] = fixes };
    }
}
