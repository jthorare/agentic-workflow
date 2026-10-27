using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// The stop's read model (scoping spec §8.4): nine sections, each built from the facts it needs and marked "not yet" with the state that
/// writes them when they are absent. The checklist re-runs the rules over the live facts; the row text of a recorded limit comes from the
/// one constant map (<see cref="ScopeFixLines"/>), so the CLI and the app cannot drift from it.
/// </summary>
internal static partial class ScopingViewBuilder
{
    private const string PacketReason = "Available once the evidence step assembles the packet";

    /// <summary>Builds the view over the facts.</summary>
    internal static ScopingView Build(ScopeFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var report = ScopePacketChecks.Run(facts, ScopeStage.Evidence);
        var packet = facts.Has("scopePacket");
        return new ScopingView(
            packet ? ScopeStage.Evidence : ScopeStage.Draft,
            Header(facts),
            new ScopingOpenInContext(packet, packet ? null : PacketReason),
            [Checklist(facts, report), Flagged(facts), Excluded(facts), Acceptance(facts), Questions(facts), Answers(facts), Challenge(facts), Rounds(facts), Map(facts)]);
    }

    private static string Header(ScopeFacts facts)
    {
        if (!facts.Has("coverage"))
        {
            return "coverage not yet gathered (written by step.scope.gather)";
        }

        var rows = ScopeJson.Items(facts["coverage"], "lenses").ToList();
        var limited = rows.Count(static r => ScopeJson.Items(r, "limits").Any());
        var lenses = string.Create(CultureInfo.InvariantCulture, $"{limited} of {ScopeRules.Lenses.Count} lenses limited");
        return string.Join("; ", new[] { lenses, ScopeCounts.Relevance(facts), ScopeCounts.Gap(facts) }.OfType<string>());
    }

    // The model checks lead the checklist (a warning there is the first thing the owner reads), in the rules' order.
    private static readonly string[] _modelRules = [ScopeRules.Chal2, ScopeRules.Rel2, ScopeRules.Gap2];

    private static bool IsModelRule(string rule) => _modelRules.Contains(rule, StringComparer.Ordinal);

    /// <summary>A section's place in the view: its order, name and heading.</summary>
    private sealed record Heading(int Order, string Name, string Title);

    private static ScopingSection Section(Heading heading, string? notYet, List<ScopingRow> rows, string empty)
    {
        if (notYet is not null)
        {
            return new ScopingSection(heading.Order, heading.Name, heading.Title, ScopingSectionState.NotYet, $"not yet: {notYet}", []);
        }

        return rows.Count == 0
            ? new ScopingSection(heading.Order, heading.Name, heading.Title, ScopingSectionState.Empty, empty, [])
            : new ScopingSection(heading.Order, heading.Name, heading.Title, ScopingSectionState.Ready, Note: null, rows);
    }

    private static string? NotYet(ScopeFacts facts, string writer, params string[] keys) => facts.Missing(keys).Count > 0 ? $"written by {writer}" : null;

    private static ScopingSection Checklist(ScopeFacts facts, ScopingCheckResponse report)
    {
        var rows = new List<ScopingRow>();
        rows.AddRange(report.Warnings.Where(static w => IsModelRule(w.Rule))
            .Select(static w => new ScopingRow(w.Rule, w.Message, "warn", [w.Fix])));
        foreach (var rule in report.Rules)
        {
            var findings = report.Findings.Where(f => string.Equals(f.Rule, rule.Rule, StringComparison.Ordinal)).ToList();
            var status = rule.Status switch
            {
                ScopeRuleStatus.Pass => "pass",
                ScopeRuleStatus.Fail => "fail",
                ScopeRuleStatus.Warn => "warned",
                ScopeRuleStatus.NotRun => "not-run",
                _ => "not-run",
            };
            var lines = findings.Select(static f => $"{f.Item ?? "-"}: {f.Message} (fix: {f.Fix})").ToList();
            if (rule.Missing.Count > 0)
            {
                lines.Add($"needs: {string.Join(", ", rule.Missing)}");
            }

            rows.Add(new ScopingRow(rule.Rule, $"{rule.Rule} {status}", status, lines));
        }

        rows.AddRange(report.Warnings.Where(static w => !IsModelRule(w.Rule))
            .Select(static w => new ScopingRow(w.Rule, w.Message, "warn", [w.Fix])));
        foreach (var row in ScopeJson.Items(facts["coverage"], "lenses"))
        {
            var lens = ScopeJson.Str(row, "lens") ?? "?";
            rows.AddRange(LimitTexts(row).Select(text => new ScopingRow(lens, $"{lens}: {text}", "limit", [])));
        }

        return new ScopingSection(1, "checklist", "Checklist", ScopingSectionState.Ready, Note: null, rows);
    }

    /// <summary>The limit texts of a coverage row, one for each limit it records, in the order its sources list them.</summary>
    private static List<string> LimitTexts(JsonNode row)
    {
        var lens = ScopeJson.Str(row, "lens") ?? string.Empty;
        var texts = new List<string>();
        var matched = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in ScopeJson.Items(ScopeJson.Obj(row, "searched"), "sources").OfType<JsonObject>())
        {
            if (ScopeFixLines.Match(lens, ScopeJson.Str(source, "state"), ScopeJson.Str(source, "reason")) is { } limit && matched.Add(limit.Name))
            {
                texts.Add(ScopeFixLines.Text(limit, source));
            }
        }

        foreach (var why in ScopeJson.Items(row, "limits").Select(static l => ScopeJson.Str(l, "why")).OfType<string>().Where(w => !matched.Contains(w)))
        {
            if (ScopeFixLines.ByName(why) is { } limit && matched.Add(limit.Name))
            {
                texts.Add(ScopeFixLines.Text(limit, source: null));
            }
        }

        return texts;
    }

    private static ScopingSection Flagged(ScopeFacts facts)
    {
        var map = facts["scopeMap"];
        var rows = ScopeJson.Items(map, "flagged").Select(static f => new ScopingRow(ScopeJson.Str(f, "item") ?? "?", $"{ScopeJson.Str(f, "item")}: {ScopeJson.Str(f, "reason")}", "flagged", []))
            .Concat(ScopeJson.Items(map, "unresolved").Select(static u =>
                new ScopingRow(ScopeJson.Str(u, "item") ?? ScopeJson.Str(u, "rule") ?? "?", $"{ScopeJson.Str(u, "rule")} {ScopeJson.Str(u, "item")}: {ScopeJson.Str(u, "why")}", "unresolved", []))).ToList();
        var notYet = NotYet(facts, "step.scope.draft", "scopeMap");
        var checks = ReviewRows(facts, written: notYet is null);
        // why: the relevance and gap search rows don't wait for the packet; they show whenever their facts are there
        return Section(new Heading(2, "flagged", "Flagged items and unresolved findings"), notYet is not null && checks.Count == 0 ? notYet : null, [.. rows, .. checks], "No flagged items");
    }

    /// <summary>The rows the relevance review and the gap search add: each disputed entry, each candidate the judge could not rule on, each confirmed gap of the last round, or (once the draft is written) the Step that writes a fact still absent.</summary>
    private static List<ScopingRow> ReviewRows(ScopeFacts facts, bool written)
    {
        var rows = new List<ScopingRow>();
        if (!facts.Has("relevance") && written)
        {
            rows.Add(new ScopingRow("relevance", "not yet: written by step.scope.relevance-apply", "not-yet", []));
        }

        rows.AddRange(DisputedRows(facts));

        if (!facts.Has("gapSearch"))
        {
            if (written)
            {
                rows.Add(new ScopingRow("gap-search", "not yet: written by step.scope.gap-log", "not-yet", []));
            }

            return rows;
        }

        rows.AddRange(GapRows(facts));

        return rows;
    }

    private static ScopingSection Excluded(ScopeFacts facts)
    {
        var map = facts["scopeMap"];
        var rows = ScopeJson.Items(map, "excluded").Select(static e => new ScopingRow(ScopeJson.Str(e, "item") ?? "?", $"{ScopeJson.Str(e, "item")}: {ScopeJson.Str(e, "reason")}", "excluded", []))
            .Concat(ScopeJson.Items(map, "unmapped").Select(static u => new ScopingRow(ScopeJson.Str(u, "path") ?? "?", $"{ScopeJson.Str(u, "path")}: {ScopeJson.Str(u, "reason")}", "unmapped", []))).ToList();
        return Section(new Heading(3, "excluded", "Excluded surfaces and unmapped files"), NotYet(facts, "step.scope.draft", "scopeMap"), rows, "No excluded surfaces");
    }

    private static ScopingSection Acceptance(ScopeFacts facts)
    {
        var log = ScopeJson.Items(facts["answerLog"]).ToList();
        var rows = new List<ScopingRow>();
        foreach (var item in ScopeJson.Items(facts["acceptance"], "items"))
        {
            var id = ScopeJson.Str(item, "id") ?? "?";
            var citations = ScopeJson.Strings(item, "citations").ToList();
            var lines = new List<string> { $"cites: {string.Join(", ", citations)}" };
            var behind = log.Where(e => citations.Contains(ScopeJson.Str(e, "id") ?? string.Empty, StringComparer.Ordinal)
                || ScopeJson.Strings(e, "recordedAs").Contains(id, StringComparer.Ordinal));
            lines.AddRange(behind.SelectMany(e => ScopeJson.Items(e, "answers").Select(a => $"answer {ScopeJson.Str(e, "id")}: {Answer(a)}")));
            rows.Add(new ScopingRow(id, $"{id} [{ScopeJson.Str(item, "form")}] {ScopeJson.Str(item, "text")}", ScopeJson.Str(item, "status") ?? "failing", lines));
        }

        return Section(new Heading(4, "acceptance", "Acceptance items"), NotYet(facts, "step.scope.draft", "acceptance"), rows, "No acceptance items");
    }

    private static string Answer(JsonNode answer) =>
        ScopeJson.Str(answer, "other") is { } other
            ? $"{ScopeJson.Str(answer, "question")} other: {other}"
            : $"{ScopeJson.Str(answer, "question")} {ScopeJson.Str(answer, "option")} {ScopeJson.Str(answer, "text")}".TrimEnd();

    private static ScopingSection Questions(ScopeFacts facts)
    {
        var rows = ScopeJson.Items(facts["openQuestions"]).Select(static q =>
        {
            var id = ScopeJson.Str(q, "id") ?? "?";
            var lines = ScopeJson.Str(q, "recommended") is { } recommended ? new List<string> { $"recommended: {recommended}" } : [];
            return new ScopingRow(id, $"{id} (impact {ScopeJson.Int(q, "impact")?.ToString(CultureInfo.InvariantCulture) ?? "?"}): {ScopeJson.Str(q, "question")}", Status: null, lines);
        }).ToList();
        return Section(new Heading(5, "questions", "Open questions"), NotYet(facts, "step.interview.questions", "openQuestions"), rows, "No open questions");
    }

    private static ScopingSection Answers(ScopeFacts facts)
    {
        var rows = ScopeJson.Items(facts["answerLog"]).Reverse().Select(static e =>
        {
            var id = ScopeJson.Str(e, "id") ?? "?";
            var answers = ScopeJson.Items(e, "answers").ToList();
            return new ScopingRow(id, $"{id} round {ScopeJson.Int(e, "round")?.ToString(CultureInfo.InvariantCulture) ?? "?"}: {answers.Count.ToString(CultureInfo.InvariantCulture)} answers", Status: null, [.. answers.Select(Answer)]);
        }).ToList();
        return Section(new Heading(6, "answers", "Answers, last batch first"), NotYet(facts, "step.interview.record", "answerLog"), rows, "No answers yet");
    }

    private static ScopingSection Challenge(ScopeFacts facts)
    {
        var runs = facts["runs"] is JsonArray all ? all : ScopeJson.Arr(facts["runs"], "runs");
        var challengers = ScopeJson.Items(runs).Where(static r => string.Equals(ScopeJson.Str(r, "step"), "step.scope.challenge", StringComparison.Ordinal)).ToList();
        var rows = new List<ScopingRow>();
        var n = 0;
        foreach (var round in ScopeJson.Items(facts["challenge"], "rounds"))
        {
            var run = n < challengers.Count ? challengers[n] : null;
            n++;
            var who = run is null ? "run not recorded" : $"model {ScopeJson.Str(run, "model") ?? "not recorded"}, vendor {ScopeJson.Str(run, "vendor") ?? "not recorded"}, via {ScopeJson.Str(run, "via") ?? "?"}";
            var counts = $"surfaces {ScopeJson.Items(round, "surfaces").Count().ToString(CultureInfo.InvariantCulture)}, behaviours {ScopeJson.Items(round, "behaviours").Count().ToString(CultureInfo.InvariantCulture)}";
            var key = string.Create(CultureInfo.InvariantCulture, $"round-{n}");
            rows.Add(new ScopingRow(key, $"round {n.ToString(CultureInfo.InvariantCulture)}: {ScopeJson.Str(round, "verdict")} ({counts}) — {who}", ScopeJson.Str(round, "verdict"), []));
        }

        return Section(new Heading(7, "challenge", "Challenger rounds"), NotYet(facts, "step.scope.challenge-log", "challenge"), rows, "The challenger has not run");
    }

    private static ScopingSection Rounds(ScopeFacts facts)
    {
        var rows = ScopeJson.Items(facts["rounds"]).Select(static r =>
        {
            var n = ScopeJson.Int(r, "round")?.ToString(CultureInfo.InvariantCulture) ?? "?";
            var text = $"round {n} ({ScopeJson.Str(r, "trigger")}): +{ScopeJson.Items(r, "added").Count().ToString(CultureInfo.InvariantCulture)} -{ScopeJson.Items(r, "removed").Count().ToString(CultureInfo.InvariantCulture)} ~{ScopeJson.Items(r, "changed").Count().ToString(CultureInfo.InvariantCulture)} — {ScopeJson.Str(r, "cause")}";
            return new ScopingRow($"round-{n}", text, Status: null, []);
        }).ToList();
        return Section(new Heading(8, "rounds", "Draft rounds"), NotYet(facts, "step.scope.check", "rounds"), rows, "No draft rounds yet");
    }

    private static ScopingSection Map(ScopeFacts facts)
    {
        var rows = new List<ScopingRow>();
        foreach (var surface in ScopeJson.Items(facts["scopeMap"], "surfaces"))
        {
            var status = ScopeJson.Str(surface, "graphStatus") ?? "unknown";
            var glyph = status switch { "mapped" => "▣", "new" => "◇", "unmapped" => "░", _ => "?" };
            var id = ScopeJson.Str(surface, "id") ?? "?";
            rows.Add(new ScopingRow(ScopeJson.Str(surface, "key") ?? id, $"{glyph} {id} {ScopeJson.Str(surface, "key")} — {ScopeJson.Str(surface, "title")}", status, []));
        }

        foreach (var row in ScopeJson.Items(facts["coverage"], "lenses"))
        {
            rows.Add(CoverageRow(row));
        }

        var missing = facts.Missing("scopeMap", "coverage");
        return missing.Count == 2
            ? Section(new Heading(9, "map", "Scope map and coverage"), "written by step.scope.draft and step.scope.gather", [], string.Empty)
            : Section(new Heading(9, "map", "Scope map and coverage"), notYet: null, rows, "The map is empty");
    }

    private static ScopingRow CoverageRow(JsonNode row)
    {
        var lens = ScopeJson.Str(row, "lens") ?? "?";
        var count = ScopeJson.Int(ScopeJson.Obj(row, "found"), "count") ?? 0;
        var limits = LimitTexts(row);
        var state = ScopeJson.Str(row, "state");
        var first = state switch
        {
            "found" => $"found {count.ToString(CultureInfo.InvariantCulture)}",
            "nothing-found" => "nothing found",
            "not-applicable" => $"not applicable: {ScopeJson.Str(row, "reason")}",
            _ => limits.Count > 0 ? limits[0] : "not searched",
        };
        var rest = string.Equals(state, "not-searched", StringComparison.Ordinal) && limits.Count > 0 ? limits.Skip(1) : limits;
        return new ScopingRow($"coverage:{lens}", first, state, [.. rest]);
    }
}
