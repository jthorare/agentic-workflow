using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

internal static partial class ScopePacketChecks
{
    private const double MapCoverageFloor = 0.5;

    /// <summary>The coverage rows, in the order the fact holds them.</summary>
    private static IEnumerable<JsonNode> Rows(ScopeContext c) => ScopeJson.Items(c.Facts["coverage"], "lenses");

    /// <summary>SCP-COV-001: one row per lens.</summary>
    private static void CoverageRows(ScopeContext c, RuleSink s)
    {
        if (!s.Need("coverage"))
        {
            return;
        }

        var rows = Rows(c).Select(static r => ScopeJson.Str(r, "lens")).ToList();
        foreach (var lens in ScopeRules.Lenses)
        {
            var count = rows.Count(r => string.Equals(r, lens, StringComparison.Ordinal));
            if (count != 1)
            {
                s.Fail(lens, count == 0 ? $"fact:coverage has no row for the {lens} lens" : $"fact:coverage has {count.ToString(CultureInfo.InvariantCulture)} rows for the {lens} lens", "Write exactly one row for each of code, product, related and history.");
            }
        }

        foreach (var other in rows.Where(r => r is null || !ScopeRules.Lenses.Contains(r, StringComparer.Ordinal)))
        {
            s.Fail(other, $"fact:coverage has a row for an unknown lens '{other}'", "Use the lens names code, product, related and history.");
        }
    }

    /// <summary>SCP-COV-002: each row's state matches what it searched; only the recorded limits are tolerated, a degraded source never is.</summary>
    private static void CoverageStates(ScopeContext c, RuleSink s)
    {
        if (!s.Need("coverage"))
        {
            return;
        }

        foreach (var row in Rows(c))
        {
            var lens = ScopeJson.Str(row, "lens") ?? "?";
            var recorded = RecordedFrom(row, lens, s);
            StateAgrees(row, lens, recorded, s);
            LimitsAgree(row, lens, recorded, s);
        }
    }

    /// <summary>The limits the row's sources record (the table matches on lens, state and reason); every other non-ok source is a finding.</summary>
    private static List<string> RecordedFrom(JsonNode row, string lens, RuleSink s)
    {
        var recorded = new List<string>();
        foreach (var source in ScopeJson.Items(ScopeJson.Obj(row, "searched"), "sources").OfType<JsonObject>())
        {
            var state = ScopeJson.Str(source, "state");
            var reason = ScopeJson.Str(source, "reason");
            if (string.IsNullOrEmpty(state))
            {
                continue;
            }

            // An ok source is no fault; one with a recorded reason (catching-up) is a limit the row has to list.
            if (string.Equals(state, "ok", StringComparison.Ordinal))
            {
                if (ScopeFixLines.Match(lens, state, reason) is { } okLimit)
                {
                    recorded.Add(okLimit.Name);
                }

                continue;
            }

            var id = ScopeJson.Str(source, "id") ?? "source";
            if (string.Equals(state, "degraded", StringComparison.Ordinal))
            {
                s.Fail(lens, $"the {lens} lens's {id} is degraded, which is no recorded limit", "Run the lens again (regather); a degraded source is a fault, not a limit.");
            }
            else if (ScopeFixLines.Match(lens, state, reason) is { } limit)
            {
                recorded.Add(limit.Name);
            }
            else
            {
                s.Fail(lens, $"the {lens} lens's {id} is {state} with reason '{reason ?? "none"}', which is no recorded limit of the {lens} lens", "Use a reason the Recorded limits table has for this lens, or run the lens again (regather).");
            }
        }

        return recorded;
    }

    private static void StateAgrees(JsonNode row, string lens, List<string> recorded, RuleSink s)
    {
        var state = ScopeJson.Str(row, "state");
        var count = ScopeJson.Int(ScopeJson.Obj(row, "found"), "count") ?? 0;
        var limited = recorded.Count > 0 || ScopeJson.Items(row, "limits").Any();
        var problem = state switch
        {
            "found" => count <= 0 ? $"the {lens} row is 'found' with a count of {count.ToString(CultureInfo.InvariantCulture)}" : null,
            "nothing-found" => NothingFoundProblem(lens, count, limited),
            "not-searched" => NotSearchedProblem(lens, count, limited),
            "not-applicable" => string.IsNullOrWhiteSpace(ScopeJson.Str(row, "reason")) ? $"the {lens} row is 'not-applicable' without a reason" : null,
            _ => $"the {lens} row has the state '{state}'",
        };
        if (problem is not null)
        {
            s.Fail(lens, problem, "A row is found (count above 0), nothing-found (count 0 and every source ok), not-searched (a recorded limit) or not-applicable (with the profile's reason).");
        }
    }

    private static string? NothingFoundProblem(string lens, int count, bool limited) =>
        limited ? $"the {lens} row is 'nothing-found' but a source recorded a limit; it must be 'not-searched'"
        : count > 0 ? $"the {lens} row is 'nothing-found' with a count above 0"
        : null;

    private static string? NotSearchedProblem(string lens, int count, bool limited) =>
        !limited ? $"the {lens} row is 'not-searched' with no recorded limit"
        : count > 0 ? $"the {lens} row is 'not-searched' with a count above 0"
        : null;

    private static void LimitsAgree(JsonNode row, string lens, List<string> recorded, RuleSink s)
    {
        var listed = ScopeJson.Items(row, "limits").Select(static l => ScopeJson.Str(l, "why")).OfType<string>().ToList();
        foreach (var name in recorded.Where(r => !listed.Contains(r, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal))
        {
            s.Fail(lens, $"the {lens} row's sources record {name}, but its limits[] does not list it", "Write one limits[] entry for each source that matches a recorded limit.");
        }

        foreach (var why in listed.Where(w => !recorded.Contains(w, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal))
        {
            s.Fail(lens, ScopeFixLines.ByName(why) is null
                ? $"the {lens} row's limits[] lists '{why}', which is no recorded limit"
                : $"the {lens} row's limits[] lists {why}, but no source of the row matches it on this lens", "List only limits a source of this lens records.");
        }
    }

    /// <summary>SCP-COV-005 (warning): a row with nothing found and a thin map is shown as such.</summary>
    private static void ThinMap(ScopeContext c, RuleSink s)
    {
        if (!s.Need("coverage"))
        {
            return;
        }

        foreach (var row in Rows(c).Where(static r => string.Equals(ScopeJson.Str(r, "state"), "nothing-found", StringComparison.Ordinal)))
        {
            var coverage = ScopeJson.Obj(row, "mapCoverage");
            var mapped = ScopeJson.Int(coverage, "mapped") ?? 0;
            var total = mapped + (ScopeJson.Int(coverage, "new") ?? 0) + (ScopeJson.Int(coverage, "unmapped") ?? 0);
            if (total == 0 || (double)mapped / total < MapCoverageFloor)
            {
                var lens = ScopeJson.Str(row, "lens") ?? "?";
                s.Warn(lens, $"{lens}: nothing found, map thin: {mapped.ToString(CultureInfo.InvariantCulture)} of {total.ToString(CultureInfo.InvariantCulture)} nodes mapped", "Map the area (context map), then regather; the scope does not refuse.");
            }
        }
    }

    /// <summary>SCP-COV-003: every found node at depth 0 or 1 is in the map or listed under excluded.</summary>
    private static void LensItemsMapped(ScopeContext c, RuleSink s)
    {
        if (!s.Need("lenses", "scopeMap"))
        {
            return;
        }

        var surfaces = ScopeJson.Items(c.Facts["scopeMap"], "surfaces").ToList();
        var keys = surfaces.Select(static x => ScopeJson.Str(x, "key")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var cited = surfaces.SelectMany(static x => ScopeJson.Strings(x, "citations")).ToHashSet(StringComparer.Ordinal);
        var excluded = ScopeJson.Items(c.Facts["scopeMap"], "excluded").Select(static x => ScopeJson.Str(x, "item")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var lens in ScopeRules.Lenses)
        {
            var entry = ScopeJson.Obj(c.Facts["lenses"], lens);
            foreach (var node in NearNodes(entry))
            {
                var key = ScopeJson.Str(node, "key") ?? "?";
                var item = ScopeJson.Str(node, "lensItem") ?? "?";
                if (!keys.Contains(key) && !cited.Contains(item) && !excluded.Contains(key) && !excluded.Contains(item))
                {
                    s.Fail(key, $"{item} ({key}) is at depth 0 or 1 of the {lens} lens and is neither in the map nor excluded", "Add it to the map, or list it under excluded with a one-line reason.");
                }
            }
        }
    }

    /// <summary>The nodes of a lens at depth 0 or 1: all of them for a lens that followed at most one edge, else those within one edge of an anchor.</summary>
    private static IEnumerable<JsonNode> NearNodes(JsonObject? entry)
    {
        var nodes = ScopeJson.Items(entry, "nodes").ToList();
        if ((ScopeJson.Int(entry, "depth") ?? 0) <= 1)
        {
            return nodes;
        }

        var anchors = ScopeJson.Strings(entry, "anchors").ToHashSet(StringComparer.Ordinal);
        var near = new HashSet<string>(anchors, StringComparer.Ordinal);
        foreach (var edge in ScopeJson.Items(entry, "edges"))
        {
            var from = ScopeJson.Str(edge, "from") ?? string.Empty;
            var to = ScopeJson.Str(edge, "to") ?? string.Empty;
            if (anchors.Contains(from))
            {
                _ = near.Add(to);
            }

            if (anchors.Contains(to))
            {
                _ = near.Add(from);
            }
        }

        return nodes.Where(n => near.Contains(ScopeJson.Str(n, "key") ?? string.Empty));
    }

    /// <summary>SCP-COV-004: for a change input, every changed file is mapped or listed under unmapped.</summary>
    private static void ChangedFilesMapped(ScopeContext c, RuleSink s)
    {
        if (!s.Need("brief", "scopeMap"))
        {
            return;
        }

        var isChange = ScopeJson.Items(c.Facts["brief"], "refs").Any(static r => string.Equals(ScopeJson.Str(r, "source"), "change", StringComparison.Ordinal));
        if (!isChange)
        {
            return;
        }

        if (!s.Need("anchors"))
        {
            return;
        }

        var keys = ScopeJson.Items(c.Facts["scopeMap"], "surfaces").Select(static x => ScopeJson.Str(x, "key")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var unmapped = ScopeJson.Items(c.Facts["scopeMap"], "unmapped").Select(static x => ScopeJson.Str(x, "path")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var file in ScopeJson.Strings(c.Facts["anchors"]).Where(static a => a.StartsWith("code:", StringComparison.Ordinal)))
        {
            if (!keys.Contains(file) && !unmapped.Contains(file) && !unmapped.Contains(file["code:".Length..]))
            {
                s.Fail(file, $"changed file {file["code:".Length..]} is neither mapped nor listed under unmapped", "Map the file, or list its path under unmapped with a reason.");
            }
        }
    }
}
