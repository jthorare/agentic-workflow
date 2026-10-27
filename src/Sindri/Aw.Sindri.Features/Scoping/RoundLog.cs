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

    /// <summary>The log the facts hold, as a copy a caller can add to (empty when the fact is absent or is not an array).</summary>
    internal static JsonArray Of(ScopeFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts[Key] is JsonArray log ? (JsonArray)log.DeepClone() : [];
    }

    /// <summary>The earlier entries unchanged plus one entry for each of <paramref name="findings"/>.</summary>
    /// <param name="facts">The Step's facts, which hold the log so far.</param>
    /// <param name="step">The Step's short name ('check', 'challenge-log', 'evidence').</param>
    /// <param name="by">The Step's id.</param>
    /// <param name="findings">What the Step found this run.</param>
    /// <param name="round">The Step's round when something else numbers it; otherwise one more than its last entry's.</param>
    /// <returns>The log to write back.</returns>
    internal static JsonArray Append(ScopeFacts facts, string step, string by, IEnumerable<ScopeFinding> findings, int? round = null)
    {
        ArgumentNullException.ThrowIfNull(findings);
        var log = Of(facts);
        var number = round ?? (Latest(log, step) + 1);
        foreach (var finding in findings)
        {
            ScopeJson.Append(log, new JsonObject
            {
                ["round"] = number,
                ["step"] = step,
                ["kind"] = Finding,
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

    /// <summary>The number of the Step's last round in the log; 0 when it has no entry.</summary>
    private static int Latest(JsonArray log, string step) =>
        ScopeJson.Items(log).Where(e => string.Equals(ScopeJson.Str(e, "step"), step, StringComparison.Ordinal)).Select(static e => ScopeJson.Int(e, "round") ?? 0).DefaultIfEmpty(0).Max();

    private static bool IsEntry(JsonNode entry, string step, int round) =>
        string.Equals(ScopeJson.Str(entry, "step"), step, StringComparison.Ordinal) && ScopeJson.Int(entry, "round") == round;

    private static string KeyOf(ScopeFinding finding) => $"{finding.Rule}:{finding.Item}";
}
