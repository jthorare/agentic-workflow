using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// The block Steps the desk helper covers (scoping spec §8.2, 'Desk helper'): each runs the Step's code over the facts the Step reads and
/// returns what its desk 'complete' takes. The Steps that read the context engine's JSON (gather, anchor, reanchor, propose) take its views as facts
/// (lensesView, codeView, historyView, contextQuery, proposeResult), and read-brief takes the file's text as briefSource.
/// </summary>
internal static partial class DeskBlocks
{
    private static readonly Dictionary<string, string> _ids = new(StringComparer.Ordinal)
    {
        ["intake"] = "step.scope.intake",
        ["check"] = "step.scope.check",
        ["challenge-log"] = "step.scope.challenge-log",
        ["evidence"] = "step.scope.evidence",
        ["record"] = "step.interview.record",
        ["interview.record"] = "step.interview.record",
        ["gather"] = "step.scope.gather",
        ["read-brief"] = "step.scope.read-brief",
        ["anchor"] = "step.scope.anchor",
        ["reanchor"] = "step.scope.reanchor",
        ["propose"] = "step.scope.propose",
        ["seed"] = "step.brief.seed",
        ["brief.seed"] = "step.brief.seed",
    };

    /// <summary>The block ids the helper covers.</summary>
    internal static IReadOnlyList<string> Covered { get; } = [.. _ids.Values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <summary>The full Step id for a full or short name, or null when the helper does not cover it.</summary>
    internal static string? Resolve(string name) => _ids.Values.Contains(name, StringComparer.Ordinal) ? name : _ids.GetValueOrDefault(name);

    /// <summary>Runs the Step's code.</summary>
    /// <param name="block">The Step's id.</param>
    /// <param name="facts">The facts.</param>
    /// <param name="challengeCap">The running recipe's loops.challenge.cap; the shipped default where no recipe runs the helper (the desk).</param>
    /// <param name="gapCap">The running recipe's loops.gapsearch.cap; the shipped default where no recipe runs the helper (the desk).</param>
    internal static Result<ScopingDeskResponse> Run(string block, ScopeFacts facts, int challengeCap = ScopeRules.DefaultChallengeRounds, int gapCap = ScopeRules.DefaultGapRounds) => block switch
    {
        "step.scope.intake" => Intake(facts),
        "step.scope.check" => Check(facts),
        "step.scope.challenge-log" => ChallengeLog(facts),
        "step.scope.evidence" => Evidence(facts, challengeCap, gapCap),
        "step.interview.record" => Record(facts),
        "step.brief.seed" => Seed(),
        "step.scope.gather" => Gather(facts),
        "step.scope.read-brief" => ReadBrief(facts),
        "step.scope.read-tracker" => ReadTracker(facts),
        "step.scope.read-change" => ReadChange(facts),
        "step.scope.anchor" => Anchor(facts),
        "step.scope.reanchor" => Reanchor(facts),
        "step.scope.propose" => Propose(facts),
        _ => SindriErrors.InvalidInput.With($"no desk helper for {block}"),
    };

    private static ScopingDeskResponse Reply(string block, string outcome, IReadOnlyDictionary<string, string> outputs, IReadOnlyList<ScopingDeskFile> files, ScopingCheckResponse? report = null) =>
        new(block, outcome, outputs, files, report?.Findings ?? [], report?.Warnings ?? []);

    private static Dictionary<string, string> Outputs(params (string Key, JsonNode Value)[] facts) =>
        facts.ToDictionary(static f => f.Key, static f => ScopeJson.Write(f.Value), StringComparer.Ordinal);

    private static JsonArray FindingsFact(IEnumerable<ScopeFinding> findings)
    {
        var array = new JsonArray();
        foreach (var finding in findings)
        {
            ScopeJson.Append(array, new JsonObject { ["rule"] = finding.Rule, ["item"] = finding.Item, ["message"] = finding.Message, ["fix"] = finding.Fix, ["severity"] = "fail" });
        }

        return array;
    }

    private static Result<ScopingDeskResponse> Intake(ScopeFacts facts)
    {
        var kind = ScopeJson.Str(facts["scopeInput"], "kind");
        if (kind is not ("brief" or "tracker" or "idea" or "change"))
        {
            return SindriErrors.InvalidInput.With($"fact:scopeInput has the kind '{kind ?? "none"}'; intake sorts brief, tracker, idea and change (fix: write a scopeInput.json with one of them)");
        }

        var outputs = ScopeSchemas.Seeds().ToDictionary(static s => s.Key, static s => ScopeJson.Write(s.Value), StringComparer.Ordinal);
        return Reply("step.scope.intake", kind, outputs, []);
    }

    /// <summary>SCP-LENS-001 failing at a check names a fault in the gathered lens, which another gather reproduces and a redraft can't change: the Step fails, it neither regathers nor reports gaps.</summary>
    private static Error? LensFaultAt(string block, ScopingCheckResponse report)
    {
        var faults = report.Findings.Where(static f => string.Equals(f.Rule, ScopeRules.Lens1, StringComparison.Ordinal)).Select(static f => f.Message).ToList();
        return faults.Count == 0
            ? null
            : SindriErrors.InvalidInput.With($"{ScopeRules.Lens1} fails at {block}: {string.Join("; ", faults)}; a fault in the lens, not in the scope (fix: the context engine's lens, #993)");
    }

    private static Result<ScopingDeskResponse> Seed() =>
        Reply("step.brief.seed", "seeded", Outputs(("answers", new JsonArray()), ("answerLog", new JsonArray())), []);

    private static Result<ScopingDeskResponse> Check(ScopeFacts facts)
    {
        var report = ScopePacketChecks.Run(facts, ScopeStage.Draft);
        if (LensFaultAt("step.scope.check", report) is { } lens)
        {
            return lens;
        }

        var outputs = report.Outcome is ScopeOutcome.Gaps or ScopeOutcome.Regather
            ? Outputs(("findings", FindingsFact(report.Findings)))
            : [];
        var round = Round(facts, report);
        return Reply("step.scope.check", report.Outcome == ScopeOutcome.Pass ? "pass" : report.Outcome == ScopeOutcome.Gaps ? "gaps" : "regather", outputs,
            [new ScopingDeskFile("rounds.jsonl", ScopeJson.Write(round) + "\n", Append: true)], report);
    }

    /// <summary>The line appended to rounds.jsonl: the round, why it ran, the items added, removed or changed by id (against the last round's digest), and the cause.</summary>
    private static JsonObject Round(ScopeFacts facts, ScopingCheckResponse report)
    {
        var previous = ScopeJson.Items(facts["rounds"]).ToList();
        var digest = new JsonObject();
        foreach (var item in ScopeJson.Items(facts["acceptance"], "items"))
        {
            digest[ScopeJson.Str(item, "id") ?? "?"] = Hash(ScopeJson.Str(item, "text") ?? string.Empty);
        }

        var before = previous.Count > 0 ? ScopeJson.Obj(previous[^1], "digest") : null;
        var round = new JsonObject
        {
            ["round"] = previous.Count + 1,
            ["trigger"] = Text(facts["trigger"]) ?? (previous.Count == 0 ? "first" : "revise"),
            ["cause"] = Text(facts["cause"]) ?? (report.Findings.Count == 0 ? "no finding" : $"{report.Findings.Count.ToString(CultureInfo.InvariantCulture)} findings"),
        };
        var (added, removed, changed) = Diff(before, digest);
        round["added"] = ScopeJson.ToArray(added);
        round["removed"] = ScopeJson.ToArray(removed);
        round["changed"] = ScopeJson.ToArray(changed);
        round["digest"] = digest;
        return round;
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static (List<string> Added, List<string> Removed, List<string> Changed) Diff(JsonObject? before, JsonObject now)
    {
        var added = new List<string>();
        var changed = new List<string>();
        foreach (var (id, hash) in now)
        {
            if (before is null || !before.ContainsKey(id))
            {
                added.Add(id);
            }
            else if (!JsonNode.DeepEquals(before[id], hash))
            {
                changed.Add(id);
            }
        }

        var removed = before is null ? [] : before.Select(static p => p.Key).Where(id => !now.ContainsKey(id)).ToList();
        return (added, removed, changed);
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12];

    private static Result<ScopingDeskResponse> ChallengeLog(ScopeFacts facts)
    {
        if (facts.Missing("challenge", "challengeRound") is { Count: > 0 } missing)
        {
            return SindriErrors.InvalidInput.With($"challenge-log needs {string.Join(", ", missing)} (fix: write them into the facts directory)");
        }

        var round = facts.ObjectCopy("challengeRound");
        if (ChallengeRoundShape.LogShape(round) is { } shape)
        {
            return SindriErrors.InvalidInput.With(shape);
        }

        var verdict = ScopeJson.Str(round, "verdict");
        var surfaces = ScopeJson.Items(round, "surfaces").ToList();
        var behaviours = ScopeJson.Items(round, "behaviours").ToList();
        var found = surfaces.Count + behaviours.Count > 0;
        if (verdict is not ("nothingNew" or "found") || found != string.Equals(verdict, "found", StringComparison.Ordinal))
        {
            return SindriErrors.InvalidInput.With($"the challenger's verdict '{verdict ?? "none"}' disagrees with its items ({surfaces.Count.ToString(CultureInfo.InvariantCulture)} surfaces, {behaviours.Count.ToString(CultureInfo.InvariantCulture)} behaviours); the Step fails (fix: report found with items, or nothingNew with none)");
        }

        var log = facts.ObjectCopy("challenge");
        var rounds = ScopeJson.Arr(log, "rounds") ?? [];
        log["rounds"] = rounds;
        round["round"] = rounds.Count + 1;
        ScopeJson.Append(rounds, round);
        var findings = surfaces.Select(s => new ScopeFinding(ScopeRules.Chal1, ScopeJson.Str(s, "title"), $"the challenger found a surface the map misses: {ScopeJson.Str(s, "title")}", "Add it to the map, or exclude it with a reason, then run the challenger again.", ScopeSeverity.Fail))
            .Concat(behaviours.Select(b => new ScopeFinding(ScopeRules.Chal1, ScopeJson.Str(b, "text"), $"the challenger found a behaviour the map misses: {ScopeJson.Str(b, "text")} (question: {ScopeJson.Str(b, "question")})", "Add an acceptance item or an open question for it, then run the challenger again.", ScopeSeverity.Fail))).ToList();
        var outputs = found ? Outputs(("challenge", log), ("findings", FindingsFact(findings))) : Outputs(("challenge", log));
        return Reply("step.scope.challenge-log", verdict ?? string.Empty, outputs, []);
    }

    private static Result<ScopingDeskResponse> Record(ScopeFacts facts)
    {
        if (facts.Missing("answers", "answerLog") is { Count: > 0 } missing)
        {
            return SindriErrors.InvalidInput.With($"record needs {string.Join(", ", missing)} (fix: write them into the facts directory)");
        }

        var log = (facts.Require("answerLog").DeepClone() as JsonArray) ?? [];
        var items = ScopeJson.Items(facts["answers"]).ToList();
        // why: a batch carries its own answers[] (the latest one is this round's); a flat list is the round's answers, each logged on its own
        var batches = items.Exists(static i => ScopeJson.Arr(i, "answers") is not null) ? items.TakeLast(1) : items;
        foreach (var batch in batches)
        {
            var digest = $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ScopeJson.Write(batch))))}";
            if (!ScopeJson.Items(log).Any(e => string.Equals(ScopeJson.Str(e, "digest"), digest, StringComparison.Ordinal)))
            {
                ScopeJson.Append(log, LogEntry(batch, log.Count + 1, digest));
            }
        }

        return Reply("step.interview.record", "recorded", Outputs(("answerLog", log)), []);
    }

    private static JsonObject LogEntry(JsonNode batch, int number, string digest)
    {
        var entry = new JsonObject { ["id"] = string.Create(CultureInfo.InvariantCulture, $"A{number}") };
        foreach (var (name, value) in (batch as JsonObject ?? []).Where(static p => !string.Equals(p.Key, "id", StringComparison.Ordinal) && !string.Equals(p.Key, "digest", StringComparison.Ordinal)))
        {
            entry[name] = value?.DeepClone();
        }

        entry["digest"] = digest;
        // why: recordedAs is never defaulted here; SCP-ACC-003 finds the entry through the items, surfaces and open questions that cite its id
        return entry;
    }

    /// <summary>'fact:recommendation' for the owner's stop: approve when no question is left open, else revise (the stop's form shows it as the recommended outcome).</summary>
    private static JsonObject Recommendation(JsonNode openQuestions)
    {
        var open = ScopeJson.Items(openQuestions).Count();
        return new JsonObject
        {
            ["outcome"] = open == 0 ? "approve" : "revise",
            ["why"] = open == 0 ? "Every check passed and no question is left open." : string.Create(CultureInfo.InvariantCulture, $"{open} question(s) are still open."),
        };
    }

    private static Result<ScopingDeskResponse> Evidence(ScopeFacts facts, int challengeCap, int gapCap)
    {
        var assembled = ScopeEvidence.FlagUncited(facts);
        var report = ScopePacketChecks.Run(assembled, ScopeStage.Evidence, challengeCap, gapCap);
        if (LensFaultAt("step.scope.evidence", report) is { } lens)
        {
            return lens;
        }

        if (report.Outcome is not ScopeOutcome.Complete)
        {
            var outcome = report.Outcome == ScopeOutcome.Regather ? "regather" : "incomplete";
            var missing = report.Rules.Where(static r => r.Status == ScopeRuleStatus.NotRun).Select(static r => r.Rule).ToList();
            var findings = report.Findings.Concat(missing.Select(static r => new ScopeFinding(r, Item: null, $"{r} did not run: a fact it needs is missing", "Write the missing facts into the Step directory.", ScopeSeverity.Fail)));
            return Reply("step.scope.evidence", outcome, Outputs(("findings", FindingsFact(findings))), [], report);
        }

        if (assembled.Missing("decision", "questions") is { Count: > 0 } lacking)
        {
            return SindriErrors.InvalidInput.With($"evidence assembles size.json and questions.json from {string.Join(", ", lacking)} (fix: write them into the facts directory)");
        }

        var outputs = Outputs(
            ("scopeMap", assembled.Require("scopeMap")),
            ("acceptance", assembled.Require("acceptance")),
            ("coverage", assembled.Require("coverage")),
            ("openQuestions", assembled.Require("openQuestions")),
            ("recommendation", Recommendation(assembled.Require("openQuestions"))));
        if (assembled["runs"] is { } runs)
        {
            // why: the runs the model checks compared are the ones the packet's runs.json holds, and the freeze re-checks them from this fact
            outputs["runs"] = ScopeJson.Write(runs);
        }

        return Reply("step.scope.evidence", "complete", outputs, ScopePacket.Build(assembled), report);
    }
}
