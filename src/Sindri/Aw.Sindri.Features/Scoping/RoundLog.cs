using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// The scoping round log (#1119): 'fact:roundLog', an append-only array with one entry per finding per Step run, <c language="text">{ round, step, kind, key, text, by }</c>.
/// A Step writes the earlier entries back unchanged and adds its own, so a finding of round 1 is still readable in round 5 while 'fact:findings' keeps holding only the newest ones.
/// A Step's round is its own run count (the check's is the line it keeps in rounds.jsonl), so the log says how many times each Step has looked.
/// </summary>
internal static class RoundLog
{
    /// <summary>The fact's name without the 'fact:' prefix.</summary>
    internal const string Key = "roundLog";

    /// <summary>The kind of an entry that records a finding.</summary>
    internal const string Finding = "finding";

    /// <summary>The kind of an entry that records a finding the check stalled on (#1239): the same degraded lens twice, which a regather can't change.</summary>
    internal const string Stalled = "stalled";

    /// <summary>The log the facts hold, as a copy a caller can add to (empty when the fact is absent or is not an array).</summary>
    internal static JsonArray Of(ScopeFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts[Key] is JsonArray log ? (JsonArray)log.DeepClone() : [];
    }

    /// <summary>The digest of the log (#1119): what the next Step reads instead of the raw log, set against the map and the answers as they stand now.</summary>
    /// <param name="log">The round log.</param>
    /// <param name="scopeMap">The scope map, or null when the instance holds none yet.</param>
    /// <param name="answerLog">The answer log, or null.</param>
    /// <returns>An object with open[], closed[], counts { byRule, byKind } and decisions[]. A finding is closed by an exclusion (an item or a glob), a map row, an answer that records it, or, when its Step's newest round no longer reports it, as fixed.</returns>
    internal static JsonObject Digest(JsonArray log, JsonNode? scopeMap, JsonNode? answerLog)
    {
        ArgumentNullException.ThrowIfNull(log);
        var excluded = ScopeJson.Items(scopeMap, "excluded").ToList();
        var coverage = new Coverage(
            excluded.Select(static x => ScopeJson.Str(x, "item")).OfType<string>().ToHashSet(StringComparer.Ordinal),
            [.. excluded.Select(static x => ScopeJson.Str(x, "glob")).OfType<string>()],
            ScopeJson.Items(scopeMap, "surfaces").SelectMany(static s => new[] { ScopeJson.Str(s, "key"), ScopeJson.Str(s, "id") }.Concat(ScopeJson.Strings(s, "citations"))).OfType<string>().ToHashSet(StringComparer.Ordinal),
            ScopeJson.Items(answerLog).SelectMany(static a => ScopeJson.Strings(a, "recordedAs")).ToHashSet(StringComparer.Ordinal));
        var entries = ScopeJson.Items(log).Where(static e => string.Equals(ScopeJson.Str(e, "kind"), Finding, StringComparison.Ordinal)).ToList();
        var newest = entries.GroupBy(static e => ScopeJson.Str(e, "step") ?? string.Empty, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.Max(static e => ScopeJson.Int(e, "round") ?? 0), StringComparer.Ordinal);
        var open = new JsonArray();
        var closed = new JsonArray();
        var byRule = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var byKind = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var latest in entries.GroupBy(static e => ScopeJson.Str(e, "key") ?? string.Empty, StringComparer.Ordinal).OrderBy(static g => g.Key, StringComparer.Ordinal).Select(static g => g.MaxBy(static e => ScopeJson.Int(e, "round") ?? 0)).OfType<JsonNode>())
        {
            var (row, by) = Classify(latest, coverage, newest);
            if (by is null)
            {
                ScopeJson.Append(open, row);
                var rule = ScopeJson.Str(row, "rule") ?? string.Empty;
                byRule[rule] = byRule.GetValueOrDefault(rule) + 1;
                var kind = ScopeJson.Str(latest, "kind") ?? Finding;
                byKind[kind] = byKind.GetValueOrDefault(kind) + 1;
            }
            else
            {
                row["by"] = by;
                ScopeJson.Append(closed, row);
            }
        }

        return new JsonObject
        {
            ["open"] = open,
            ["closed"] = closed,
            ["counts"] = new JsonObject { ["byRule"] = Counts(byRule), ["byKind"] = Counts(byKind) },
            ["decisions"] = Decisions(excluded, answerLog),
        };
    }

    /// <summary>Why a Step's output may not replace the log: it drops or rewrites an entry the log held before.</summary>
    /// <param name="before">The log the Step read; null when the instance holds none.</param>
    /// <param name="after">The log the Step wrote.</param>
    /// <returns>The refusal, or null when <paramref name="after"/> keeps every earlier entry as it was and only adds.</returns>
    internal static string? Refusal(JsonNode? before, JsonNode? after)
    {
        if (before is not JsonArray earlier)
        {
            return null;
        }

        if (after is not JsonArray later || later.Count < earlier.Count)
        {
            return $"fact:{Key} may only be added to: the output has fewer entries than the {earlier.Count} the log already holds";
        }

        for (var i = 0; i < earlier.Count; i++)
        {
            if (!JsonNode.DeepEquals(earlier[i], later[i]))
            {
                return $"fact:{Key} may only be added to: the output rewrites entry {(i + 1).ToString(CultureInfo.InvariantCulture)} ({ScopeJson.Str(earlier[i], "key")}), which an earlier round wrote";
            }
        }

        return null;
    }

    // The digest row of a finding's newest entry, and what closed it (null while it is open).
    private static (JsonObject Row, string? By) Classify(JsonNode latest, Coverage coverage, Dictionary<string, int> newest)
    {
        var key = ScopeJson.Str(latest, "key") ?? string.Empty;
        var step = ScopeJson.Str(latest, "step") ?? string.Empty;
        var round = ScopeJson.Int(latest, "round") ?? 0;
        var split = key.IndexOf(':', StringComparison.Ordinal);
        var by = coverage.ClosedBy(split < 0 ? key : key[(split + 1)..]) ?? (round < newest.GetValueOrDefault(step) ? "fixed" : null);
        var row = new JsonObject { ["key"] = key, ["rule"] = split < 0 ? key : key[..split], ["step"] = step, ["round"] = round };
        if (by is null)
        {
            row["text"] = ScopeJson.Str(latest, "text");
        }

        return (row, by);
    }

    private static JsonObject Counts(SortedDictionary<string, int> counts)
    {
        var result = new JsonObject();
        foreach (var (name, count) in counts)
        {
            result[name] = count;
        }

        return result;
    }

    private static JsonArray Decisions(List<JsonNode> excluded, JsonNode? answerLog)
    {
        var decisions = new JsonArray();
        foreach (var entry in excluded)
        {
            ScopeJson.Append(decisions, new JsonObject { ["kind"] = "exclusion", ["key"] = ScopeJson.Str(entry, "item") ?? ScopeJson.Str(entry, "glob"), ["text"] = ScopeJson.Str(entry, "reason") });
        }

        foreach (var answer in ScopeJson.Items(answerLog))
        {
            ScopeJson.Append(decisions, new JsonObject { ["kind"] = "answer", ["key"] = ScopeJson.Str(answer, "id"), ["text"] = string.Join(", ", ScopeJson.Strings(answer, "questionIds")) });
        }

        return decisions;
    }

    /// <summary>What the map and the answers cover now: the items excluded one by one, the excluded globs, the map's rows and the ids the answers are recorded as.</summary>
    private sealed record Coverage(HashSet<string> Items, List<string> Globs, HashSet<string> Rows, HashSet<string> Recorded)
    {
        internal string? ClosedBy(string item) =>
            Items.Contains(item) || ScopeGlob.MatchesAny(Globs, item) ? "exclusion"
            : Rows.Contains(item) ? "map"
            : Recorded.Contains(item) ? "answer"
            : null;
    }

    /// <summary>The earlier entries unchanged plus one entry for each of <paramref name="findings"/>.</summary>
    /// <param name="facts">The Step's facts, which hold the log so far.</param>
    /// <param name="step">The Step's short name ('check', 'challenge-log', 'evidence').</param>
    /// <param name="by">The Step's id.</param>
    /// <param name="findings">What the Step found this run.</param>
    /// <param name="round">The Step's round when something else numbers it; otherwise one more than its last entry's.</param>
    /// <returns>The log to write back.</returns>
    internal static JsonArray Append(ScopeFacts facts, string step, string by, IEnumerable<ScopeFinding> findings, int? round = null) =>
        Add(facts, (step, by, Finding), findings, round);

    /// <summary>The earlier entries unchanged plus one <see cref="Stalled"/> entry for each of <paramref name="findings"/> (the check's, #1239).</summary>
    /// <param name="facts">The Step's facts, which hold the log so far.</param>
    /// <param name="findings">The named findings the check stalled on.</param>
    /// <param name="round">The check's round.</param>
    /// <returns>The log to write back.</returns>
    internal static JsonArray AppendStalled(ScopeFacts facts, IEnumerable<ScopeFinding> findings, int? round) =>
        Add(facts, ("check", "step.scope.check", Stalled), findings, round);

    private static JsonArray Add(ScopeFacts facts, (string Step, string By, string Kind) who, IEnumerable<ScopeFinding> findings, int? round)
    {
        var (step, by, kind) = who;
        ArgumentNullException.ThrowIfNull(findings);
        var log = Of(facts);
        var number = round ?? (Latest(log, step) + 1);
        foreach (var finding in findings)
        {
            ScopeJson.Append(log, new JsonObject
            {
                ["round"] = number,
                ["step"] = step,
                ["kind"] = kind,
                ["key"] = KeyOf(finding),
                ["text"] = finding.Message,
                ["by"] = by,
            });
        }

        return log;
    }

    /// <summary>Whether <paramref name="findings"/> are exactly the findings the Step's last round in the log already recorded (and that round found something): two rounds in a row that only find the same things.</summary>
    /// <param name="facts">The Step's facts, which hold the log so far.</param>
    /// <param name="step">The Step's short name.</param>
    /// <param name="findings">What the Step found this run.</param>
    /// <returns>True when the finding sets are equal.</returns>
    internal static bool Repeats(ScopeFacts facts, string step, IEnumerable<ScopeFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);
        var log = Of(facts);
        var last = Latest(log, step);
        var before = ScopeJson.Items(log).Where(e => IsEntry(e, step, last)).Select(static e => ScopeJson.Str(e, "key") ?? string.Empty).ToHashSet(StringComparer.Ordinal);
        return before.Count > 0 && before.SetEquals(findings.Select(KeyOf));
    }

    /// <summary>The (key, text) pairs the Step's last round recorded as findings; empty when it has none.</summary>
    /// <param name="facts">The Step's facts, which hold the log so far.</param>
    /// <param name="step">The Step's short name.</param>
    /// <returns>The pairs.</returns>
    internal static HashSet<(string Key, string Text)> LastFindings(ScopeFacts facts, string step)
    {
        var log = Of(facts);
        var last = Latest(log, step);
        return [.. ScopeJson.Items(log).Where(e => IsEntry(e, step, last) && string.Equals(ScopeJson.Str(e, "kind"), Finding, StringComparison.Ordinal))
            .Select(static e => (ScopeJson.Str(e, "key") ?? string.Empty, ScopeJson.Str(e, "text") ?? string.Empty))];
    }

    /// <summary>The keys of every entry of the given kind, in any round of any Step.</summary>
    /// <param name="facts">The Step's facts, which hold the log so far.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The keys.</returns>
    internal static HashSet<string> KeysOf(ScopeFacts facts, string kind) =>
        [.. ScopeJson.Items(Of(facts)).Where(e => string.Equals(ScopeJson.Str(e, "kind"), kind, StringComparison.Ordinal)).Select(static e => ScopeJson.Str(e, "key") ?? string.Empty)];

    /// <summary>The number of the Step's last round in the log; 0 when it has no entry.</summary>
    private static int Latest(JsonArray log, string step) =>
        ScopeJson.Items(log).Where(e => string.Equals(ScopeJson.Str(e, "step"), step, StringComparison.Ordinal)).Select(static e => ScopeJson.Int(e, "round") ?? 0).DefaultIfEmpty(0).Max();

    private static bool IsEntry(JsonNode entry, string step, int round) =>
        string.Equals(ScopeJson.Str(entry, "step"), step, StringComparison.Ordinal) && ScopeJson.Int(entry, "round") == round;

    internal static string KeyOf(ScopeFinding finding) => $"{finding.Rule}:{finding.Item}";
}
