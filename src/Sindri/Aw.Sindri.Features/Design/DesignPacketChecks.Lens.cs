using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

internal static partial class DesignPacketChecks
{
    private static readonly string[] _blocking = ["CRITICAL", "HIGH"];

    /// <summary>DSN-LENS-001: the last round has no CRITICAL or HIGH finding left unless the owner accepted it at the lens desk; every round ran every lens of the pack.</summary>
    internal static void LensRoundsClean(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("lens-rounds.jsonl"))
        {
            return;
        }

        var rows = LineJson.Items(facts.Json("lens-rounds.jsonl")).ToList();
        if (rows.Count == 0)
        {
            sink.Fail(item: null, "lens-rounds.jsonl holds no round", "run the lenses at least once");
            return;
        }

        var rounds = rows.GroupBy(static r => LineJson.Number(r, "round") ?? 0).OrderBy(static g => g.Key).ToList();
        EveryLensRan(facts, sink, rows, rounds);
        foreach (var finding in rounds[^1].SelectMany(static r => LineJson.Items(r, "findings")))
        {
            if (Array.IndexOf(_blocking, LineJson.Str(finding, "severity")) >= 0 && LineJson.Bool(finding, "acceptedByOwner") != true)
            {
                var id = LineJson.Str(finding, "id") ?? string.Empty;
                sink.Fail(id, $"the last round leaves {LineJson.Str(finding, "severity")} finding {id}", "fix it, or have the owner accept the risk at the lens desk");
            }
        }
    }

    private static void EveryLensRan(LineFacts facts, LineSink sink, List<JsonNode> rows, List<IGrouping<int, JsonNode>> rounds)
    {
        var packLenses = LineJson.Items(facts.Json("pack/lenses.json"), "lenses").Select(static l => LineJson.Str(l, "id")).OfType<string>().ToList();
        var expected = packLenses.Count > 0 ? packLenses : [.. rows.Select(static r => LineJson.Str(r, "lens")).OfType<string>().Distinct(StringComparer.Ordinal)];
        foreach (var round in rounds)
        {
            var ran = round.Select(static r => LineJson.Str(r, "lens")).ToHashSet(StringComparer.Ordinal);
            foreach (var lens in expected.Where(l => !ran.Contains(l)))
            {
                sink.Fail(lens, $"round {round.Key.ToString(CultureInfo.InvariantCulture)} did not run the {lens} lens", "run every lens of the pack in every round");
            }
        }
    }

    /// <summary>DSN-LENS-002 (warning): the lens model differs from the writer's, and the vendor too when the profile allows more than one.</summary>
    internal static void LensDiffersFromWriter(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("runs.json"))
        {
            return;
        }

        var runs = LineJson.Items(facts.Json("runs.json"), "runs").ToList();
        var writer = runs.Find(static r => string.Equals(LineJson.Str(r, "step"), "step.design.draft", StringComparison.Ordinal));
        var lens = runs.Find(static r => string.Equals(LineJson.Str(r, "step"), "step.lens.run", StringComparison.Ordinal));
        if (writer is null || lens is null)
        {
            sink.Warn(item: null, "runs.json records no writer run or no lens run, so the models cannot be compared", "record both runs");
            return;
        }

        if (string.Equals(LineJson.Str(writer, "model"), LineJson.Str(lens, "model"), StringComparison.Ordinal))
        {
            sink.Warn("model", $"the lens model is the writer's ({LineJson.Str(lens, "model")})", "run the lenses on another model");
        }
        else if (LineJson.Bool(facts.Json("options.json"), "multiVendor") == true && string.Equals(LineJson.Str(writer, "vendor"), LineJson.Str(lens, "vendor"), StringComparison.Ordinal))
        {
            sink.Warn("vendor", $"the lens vendor is the writer's ({LineJson.Str(lens, "vendor")})", "run the lenses on another vendor");
        }
    }

    /// <summary>DSN-LENS-003: every finding of every round has a written resolution.</summary>
    internal static void LensFindingsResolved(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("lens-rounds.jsonl"))
        {
            return;
        }

        foreach (var finding in LineJson.Items(facts.Json("lens-rounds.jsonl")).SelectMany(static r => LineJson.Items(r, "findings")))
        {
            if (string.IsNullOrWhiteSpace(LineJson.Str(finding, "resolution")))
            {
                var id = LineJson.Str(finding, "id") ?? string.Empty;
                sink.Fail(id, $"finding {id} ({LineJson.Str(finding, "severity")}) has no written resolution", "write how it was fixed or why it was not");
            }
        }
    }

    /// <summary>DSN-ESC-001 (warning): items and surfaces the scope packet lacks are listed in escapes.json.</summary>
    internal static void EscapesListed(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("acceptance.json", ScopeAcceptance, ScopeMap, "escapes.json"))
        {
            return;
        }

        var scopeItems = Ids(LineJson.Items(facts.Json(ScopeAcceptance), "items"));
        var scopeSurfaces = ScopeSurfaces(facts).Select(static s => s.Key).ToHashSet(StringComparer.Ordinal);
        var escapes = LineJson.Items(facts.Json("escapes.json"), "escapes").ToList();
        bool Listed(string kind, string id) => escapes.Exists(e => string.Equals(LineJson.Str(e, "kind"), kind, StringComparison.Ordinal) && string.Equals(LineJson.Str(e, "id"), id, StringComparison.Ordinal));
        foreach (var item in Items(facts))
        {
            var id = LineJson.Str(item, "id") ?? string.Empty;
            if (!scopeItems.Contains(id) && !Listed("acceptance", id))
            {
                sink.Warn(id, $"{id} is not in the scope packet and is not listed as an escape", "list it in escapes.json");
            }

            foreach (var surface in LineJson.Strings(item, "surfaces").Where(s => !scopeSurfaces.Contains(s) && !Listed("surface", s)))
            {
                sink.Warn(surface, $"{surface} (used by {id}) is not in the scope map and is not listed as an escape", "list it in escapes.json");
            }
        }
    }
}
