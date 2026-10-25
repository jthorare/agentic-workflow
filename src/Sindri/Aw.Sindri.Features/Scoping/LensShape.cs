using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// The lens-shape part of SCP-LENS-001, one pure function over a lens's entry (from fact:lenses) and its coverage row: the searched anchors, depth, mapVersion and hops agree,
/// the found count and ids agree, and every edge ends at a found node or an anchor. The check reports each fault as a finding; gather runs it over its own output and fails the Step on any.
/// </summary>
internal static class LensShape
{
    /// <summary>How many example edges a fault names.</summary>
    internal const int Examples = 3;

    /// <summary>The name gather's failure carries (pending the context engine's agreement on it).</summary>
    internal const string GatherRule = "SCP-GATHER-001";

    private const string NotTheScope = "a fault in the lens, not in the scope (fix: the context engine's lens keeps only edges among its found nodes and anchors, and agrees with its coverage row, #993)";

    /// <summary>The failure message of a gather whose own lenses and coverage break the lens-shape rules, one line per lens, or null when they hold.</summary>
    /// <param name="lenses">The lenses fact gather built.</param>
    /// <param name="coverage">The coverage fact gather built.</param>
    internal static string? GatherFault(JsonNode lenses, JsonNode coverage)
    {
        ArgumentNullException.ThrowIfNull(lenses);
        ArgumentNullException.ThrowIfNull(coverage);
        var lines = new List<string>();
        foreach (var lens in ScopeRules.Lenses)
        {
            var row = ScopeJson.Items(coverage, "lenses").FirstOrDefault(r => string.Equals(ScopeJson.Str(r, "lens"), lens, StringComparison.Ordinal));
            if (ScopeJson.Obj(lenses, lens) is { } entry && row is not null && Faults(lens, entry, row) is { Count: > 0 } faults)
            {
                lines.Add(string.Join("; ", faults.Select(static f => f.Message)) + "; " + NotTheScope);
            }
        }

        return lines.Count == 0 ? null : $"{GatherRule}: " + string.Join('\n', lines);
    }

    /// <summary>The faults of one lens, none when its entry and row agree.</summary>
    /// <param name="lens">The lens's name.</param>
    /// <param name="entry">Its entry in fact:lenses.</param>
    /// <param name="row">Its row in fact:coverage.</param>
    internal static List<LensFault> Faults(string lens, JsonObject entry, JsonNode row)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(row);
        var faults = new List<LensFault>();
        Searched(lens, entry, ScopeJson.Obj(row, "searched"), faults);
        FoundIds(lens, entry, row, faults);
        Edges(lens, entry, faults);
        return faults;
    }

    private static void Searched(string lens, JsonObject entry, JsonObject? searched, List<LensFault> faults)
    {
        if (!ScopeJson.Strings(entry, "anchors").SequenceEqual(ScopeJson.Strings(searched, "anchors"), StringComparer.Ordinal))
        {
            faults.Add(new LensFault($"the {lens} lens's anchors differ from its coverage row's searched anchors", "Copy the anchors from the lens result into both."));
        }

        if (ScopeJson.Int(entry, "depth") != ScopeJson.Int(searched, "depth"))
        {
            faults.Add(new LensFault($"the {lens} lens's depth differs from its coverage row's searched depth", "Copy the depth from the lens result into both."));
        }

        if (!string.Equals(ScopeJson.Str(entry, "mapVersion"), ScopeJson.Str(searched, "mapVersion"), StringComparison.Ordinal))
        {
            faults.Add(new LensFault($"the {lens} lens's mapVersion differs from its coverage row's searched mapVersion", "Copy the map version from the lens result into both."));
        }

        if (!ScopeJson.Strings(entry, "hops").SequenceEqual(ScopeJson.Strings(searched, "hops"), StringComparer.Ordinal))
        {
            faults.Add(new LensFault($"the {lens} lens's hops differ from its coverage row's searched hops (the order is the cascade's)", "Copy the hops, in order, from the lens result into both."));
        }
    }

    private static void FoundIds(string lens, JsonObject entry, JsonNode row, List<LensFault> faults)
    {
        var held = ScopeIds.LensItems(new JsonObject { [lens] = entry.DeepClone() });
        var found = ScopeJson.Strings(ScopeJson.Obj(row, "found"), "ids").ToHashSet(StringComparer.Ordinal);
        var count = ScopeJson.Int(ScopeJson.Obj(row, "found"), "count") ?? 0;
        var truncated = ScopeJson.Items(entry, "truncated").Any();
        if (ScopeJson.Int(entry, "foundCount") != count)
        {
            faults.Add(new LensFault($"the {lens} lens's foundCount differs from its coverage row's found count", "foundCount is the full count the lens returned."));
        }

        var stray = held.Where(id => !found.Contains(id)).ToList();
        var absent = found.Where(id => !held.Contains(id)).ToList();
        if (stray.Count > 0 || (!truncated && absent.Count > 0))
        {
            faults.Add(new LensFault($"the {lens} lens's ids differ from its coverage row's found ids ({string.Join(", ", stray.Concat(absent).Take(5))})", "A lens file holds the row's found ids."));
        }
    }

    private static void Edges(string lens, JsonObject entry, List<LensFault> faults)
    {
        var ends = ScopeJson.Items(entry, "nodes").Select(static n => ScopeJson.Str(n, "key")).OfType<string>().Concat(ScopeJson.Strings(entry, "anchors")).ToHashSet(StringComparer.Ordinal);
        var dangling = new List<string>();
        var count = 0;
        foreach (var edge in ScopeJson.Items(entry, "edges"))
        {
            var from = ScopeJson.Str(edge, "from");
            var to = ScopeJson.Str(edge, "to");
            if (from is not null && to is not null && ends.Contains(from) && ends.Contains(to))
            {
                continue;
            }

            count++;
            if (dangling.Count < Examples)
            {
                dangling.Add($"{from ?? "?"} -> {to ?? "?"}");
            }
        }

        if (count > 0)
        {
            faults.Add(new LensFault($"the {lens} lens served {count.ToString(CultureInfo.InvariantCulture)} edges that end outside its found nodes and anchors (e.g. {string.Join("; ", dangling)})", "Keep only edges among the found nodes and the anchors."));
        }
    }
}
