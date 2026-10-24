using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

internal static partial class ScopePacketChecks
{
    /// <summary>SCP-LENS-001: lenses.json (from fact:lenses) agrees with the coverage rows, and its edges end at found nodes or anchors.</summary>
    private static void LensesMatchCoverage(ScopeContext c, RuleSink s)
    {
        if (!s.Need("lenses", "coverage"))
        {
            return;
        }

        foreach (var lens in ScopeRules.Lenses)
        {
            var entry = ScopeJson.Obj(c.Facts["lenses"], lens);
            var row = Rows(c).FirstOrDefault(r => string.Equals(ScopeJson.Str(r, "lens"), lens, StringComparison.Ordinal));
            if (entry is null || row is null)
            {
                s.Fail(lens, entry is null ? $"fact:lenses has no entry for the {lens} lens" : $"fact:coverage has no row for the {lens} lens", "Write one entry in fact:lenses and one row in fact:coverage for each lens.");
                continue;
            }

            Searched(lens, entry, ScopeJson.Obj(row, "searched"), s);
            FoundIds(lens, entry, row, s);
            Edges(lens, entry, s);
        }
    }

    private static void Searched(string lens, JsonObject entry, JsonObject? searched, RuleSink s)
    {
        if (!ScopeJson.Strings(entry, "anchors").SequenceEqual(ScopeJson.Strings(searched, "anchors"), StringComparer.Ordinal))
        {
            s.Fail(lens, $"the {lens} lens's anchors differ from its coverage row's searched anchors", "Copy the anchors from the lens result into both.");
        }

        if (ScopeJson.Int(entry, "depth") != ScopeJson.Int(searched, "depth"))
        {
            s.Fail(lens, $"the {lens} lens's depth differs from its coverage row's searched depth", "Copy the depth from the lens result into both.");
        }

        if (!string.Equals(ScopeJson.Str(entry, "mapVersion"), ScopeJson.Str(searched, "mapVersion"), StringComparison.Ordinal))
        {
            s.Fail(lens, $"the {lens} lens's mapVersion differs from its coverage row's searched mapVersion", "Copy the map version from the lens result into both.");
        }

        if (!ScopeJson.Strings(entry, "hops").SequenceEqual(ScopeJson.Strings(searched, "hops"), StringComparer.Ordinal))
        {
            s.Fail(lens, $"the {lens} lens's hops differ from its coverage row's searched hops (the order is the cascade's)", "Copy the hops, in order, from the lens result into both.");
        }
    }

    private static void FoundIds(string lens, JsonObject entry, JsonNode row, RuleSink s)
    {
        var held = ScopeIds.LensItems(new JsonObject { [lens] = entry.DeepClone() });
        var found = ScopeJson.Strings(ScopeJson.Obj(row, "found"), "ids").ToHashSet(StringComparer.Ordinal);
        var count = ScopeJson.Int(ScopeJson.Obj(row, "found"), "count") ?? 0;
        var truncated = ScopeJson.Items(entry, "truncated").Any();
        if (ScopeJson.Int(entry, "foundCount") != count)
        {
            s.Fail(lens, $"the {lens} lens's foundCount differs from its coverage row's found count", "foundCount is the full count the lens returned.");
        }

        var stray = held.Where(id => !found.Contains(id)).ToList();
        var absent = found.Where(id => !held.Contains(id)).ToList();
        if (stray.Count > 0 || (!truncated && absent.Count > 0))
        {
            s.Fail(lens, $"the {lens} lens's ids differ from its coverage row's found ids ({string.Join(", ", stray.Concat(absent).Take(5))})", "A lens file holds the row's found ids.");
        }
    }

    private static void Edges(string lens, JsonObject entry, RuleSink s)
    {
        var ends = ScopeJson.Items(entry, "nodes").Select(static n => ScopeJson.Str(n, "key")).OfType<string>().Concat(ScopeJson.Strings(entry, "anchors")).ToHashSet(StringComparer.Ordinal);
        foreach (var edge in ScopeJson.Items(entry, "edges"))
        {
            var from = ScopeJson.Str(edge, "from");
            var to = ScopeJson.Str(edge, "to");
            if (from is null || to is null || !ends.Contains(from) || !ends.Contains(to))
            {
                s.Fail(lens, $"an edge of the {lens} lens ({from ?? "?"} to {to ?? "?"}) ends outside its found nodes and anchors", "Keep only edges among the found nodes and the anchors.");
            }
        }
    }
}
