using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

internal static partial class DesignStopViewBuilder
{
    // The findings the owner accepted as known risks, from the last round.
    private static List<StopAccepted> Accepted(LineFacts design)
    {
        var rows = LineJson.Items(design.Json("lens-rounds.jsonl")).ToList();
        var last = rows.Select(static r => LineJson.Number(r, "round") ?? 0).DefaultIfEmpty(0).Max();
        var accepted = new List<StopAccepted>();
        foreach (var row in rows.Where(r => (LineJson.Number(r, "round") ?? 0) == last))
        {
            accepted.AddRange(LineJson.Items(row, "findings")
                .Where(static f => LineJson.Bool(f, "acceptedByOwner") == true)
                .Select(f => new StopAccepted(Text(f, "id"), Text(row, "lens"), Text(f, "severity"), Text(f, "cite"), Text(f, "text"), Text(f, "resolution"))));
        }

        return accepted;
    }

    private static StopModels Models(LineFacts design)
    {
        var runs = LineJson.Items(design.Json("runs.json"), "runs").ToList();
        var writer = runs.Find(static r => LineJson.Str(r, "step") == "step.design.draft");
        var lens = runs.Find(static r => LineJson.Str(r, "step") == "step.lens.run");
        var warnings = design.Has("runs.json")
            ? DesignPacketChecks.Run(design, LineStage.Evidence).Warnings.Where(static w => w.Rule == DesignRules.Lens2).Select(static w => w.Message).ToList()
            : [];
        return new StopModels(Model(writer), Model(lens), warnings);
    }

    private static StopModel? Model(JsonNode? run) => run is not null && LineJson.Str(run, "model") is { } model ? new StopModel(model, LineJson.Str(run, "vendor")) : null;

    private static List<StopDecision> Decisions(LineFacts design)
    {
        var decisions = new List<StopDecision>();
        foreach (var decision in LineJson.Items(design.Json("decisions.json"), "decisions"))
        {
            var options = LineJson.Items(decision, "options").Select(static o => new StopOption(Text(o, "id"), Text(o, "answer"), Text(o, "tradeoffs"), LineJson.Bool(o, "recommended") == true)).ToList();
            decisions.Add(new StopDecision(
                Text(decision, "id"),
                Text(decision, "question"),
                Text(decision, "status"),
                options.Find(static o => o.Recommended)?.Option ?? string.Empty,
                LineJson.Str(decision, "chosen"),
                [.. LineJson.Strings(decision, "affects")],
                [.. LineJson.Strings(decision, "cites")],
                options));
        }

        return decisions;
    }

    // An item is added when the scope packet lacks it, or, with no scope packet beside the design, when it says it was added by the design.
    private static StopAcceptance Acceptance(LineFacts design)
    {
        var scope = design.Has("scope/acceptance.json") ? LineJson.Items(design.Json("scope/acceptance.json"), "items").Select(static i => LineJson.Str(i, "id")).OfType<string>().ToHashSet(StringComparer.Ordinal) : null;
        var added = new List<StopItem>();
        var superseded = new List<StopSuperseded>();
        foreach (var item in LineJson.Items(design.Json("acceptance.json"), "items"))
        {
            var id = Text(item, "id");
            if (string.Equals(LineJson.Str(item, "status"), "superseded", StringComparison.Ordinal))
            {
                superseded.Add(new StopSuperseded(id, Text(item, "text"), Text(item, "supersededBy")));
            }

            if (scope is not null ? !scope.Contains(id) : !string.IsNullOrEmpty(LineJson.Str(item, "addedBy")))
            {
                added.Add(new StopItem(id, Text(item, "text"), [.. LineJson.Strings(item, "citations")], LineJson.Str(item, "trust") ?? "untrusted"));
            }
        }

        return new StopAcceptance(added, superseded);
    }

    private static List<StopEscape> Escapes(LineFacts design) =>
        [.. LineJson.Items(design.Json("escapes.json"), "escapes").Select(static e => new StopEscape(Text(e, "from"), Text(e, "station"), Text(e, "kind"), Text(e, "id"), Text(e, "why")))];

    private static List<StopCoverageRow> Coverage(LineFacts design)
    {
        var coverage = design.Json("coverage.json");
        var rows = LineJson.Items(coverage, "surfaces").Select(static s => new StopCoverageRow(Text(s, "id"), Text(s, "key"), [.. LineJson.Strings(s, "sections")], Excluded: null)).ToList();
        rows.AddRange(LineJson.Items(coverage, "excluded").Select(static s => new StopCoverageRow(Text(s, "id"), Text(s, "key"), [], Text(s, "reason"))));
        return rows;
    }

    private static StopSpec Spec(LineFacts design)
    {
        var assumes = new List<StopAssumes>();
        foreach (var section in LineJson.Items(design.Json("sections.json"), "sections"))
        {
            foreach (var option in LineJson.Strings(section, "assumes"))
            {
                assumes.Add(new StopAssumes(Text(section, "id"), option.Split('.')[0], option));
            }
        }

        return new StopSpec(design.Text("spec.md"), assumes);
    }

    private static List<StopContract> Contracts(LineFacts design)
    {
        var report = DesignPacketChecks.Run(design, LineStage.Draft);
        var contracts = new List<StopContract>();
        foreach (var edit in LineJson.Items(design.Json("contracts.json"), "edits"))
        {
            var id = Text(edit, "id");
            var checks = new[] { DesignRules.Xspec1, DesignRules.Xspec2, DesignRules.Xspec3 }.Select(rule => new StopCheck(rule, Status(report, rule, id))).ToList();
            var target = LineJson.Obj(edit, "target");
            contracts.Add(new StopContract(id, Text(edit, "lane"), Text(target, "file"), Text(target, "heading"), Text(edit, "quote"), Text(edit, "change"), LineJson.Number(edit, "row") ?? 0, checks));
        }

        return contracts;
    }

    private static string Status(LineCheckResponse report, string rule, string item) =>
        report.Rules.First(r => r.Rule == rule).Status == LineRuleStatus.NotRun ? "not-run"
        : report.Findings.Any(f => f.Rule == rule && f.Item == item) ? "fail" : "pass";

    private static List<StopLensRound> LensRounds(LineFacts design)
    {
        var rounds = new List<StopLensRound>();
        foreach (var round in LineJson.Items(design.Json("lens-rounds.jsonl")).GroupBy(static r => LineJson.Number(r, "round") ?? 0).OrderBy(static g => g.Key))
        {
            rounds.Add(new StopLensRound(round.Key, [.. round.Select(Row)]));
        }

        return rounds;
    }

    private static StopLensRow Row(JsonNode row) =>
        new(Text(row, "lens"), Text(row, "model"), LineJson.Str(row, "notApplicable"), [.. LineJson.Items(row, "findings").Select(static f => new StopFinding(Text(f, "id"), Text(f, "severity"), Text(f, "cite"), Text(f, "text"), LineJson.Str(f, "resolution")))]);
}
