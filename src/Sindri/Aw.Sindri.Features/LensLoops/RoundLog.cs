using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.LensLoops;

/// <summary>
/// A lens loop's log after one report (line spec §4.1, §5.2): the report appended to 'fact:lensRounds' (append-only, one entry a lens run, numbered by round,
/// the round's last entry marked 'closes'), the lens taken off 'fact:lensQueue', and the findings of the round so far, each with its lens.
/// </summary>
internal sealed class RoundLog
{
    private readonly LensScale _scale;
    private readonly List<JsonObject> _findings;

    private RoundLog(LensScale scale, JsonArray rounds, JsonArray queue, int round, List<JsonObject> findings)
    {
        _scale = scale;
        Rounds = rounds;
        Queue = queue;
        Round = round;
        _findings = findings;
    }

    /// <summary>The log with the report appended.</summary>
    internal JsonArray Rounds { get; }

    /// <summary>The lenses still queued.</summary>
    internal JsonArray Queue { get; }

    /// <summary>The round the report belongs to, counted from 1.</summary>
    internal int Round { get; }

    /// <summary>Whether the report was the round's last: the queue is empty.</summary>
    internal bool Ended => Queue.Count == 0;

    /// <summary>Every finding of the round so far, each with its lens.</summary>
    internal IReadOnlyList<JsonObject> Findings => _findings;

    /// <summary>The round's findings of one severity.</summary>
    /// <param name="rank">The severity's place on the scale.</param>
    /// <returns>The findings.</returns>
    internal List<JsonObject> At(int rank) => [.. _findings.Where(f => _scale.Rank(LoopJson.Str(f, "severity")) == rank)];

    /// <summary>The round's findings that stop the loop: the most severe <see cref="LensScale.Stops"/> severities.</summary>
    /// <returns>The findings.</returns>
    internal List<JsonObject> Stopping() => [.. _findings.Where(f => _scale.Rank(LoopJson.Str(f, "severity")) is >= 0 and < LensScale.Stops)];

    /// <summary>Appends the lens report to the log and takes its lens off the queue.</summary>
    /// <param name="scale">The loop's severity scale.</param>
    /// <param name="facts">The facts' JSON text by name: 'lensReport', 'lensRounds' and 'lensQueue'; a fact the Step was not given is absent.</param>
    /// <returns>The log, or why the report cannot be logged.</returns>
    internal static Result<RoundLog> Append(LensScale scale, IReadOnlyDictionary<string, string> facts)
    {
        var nodes = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var name in new[] { "lensReport", "lensRounds", "lensQueue" })
        {
            var (node, broken) = LoopJson.Parse(facts.GetValueOrDefault(name));
            if (broken is not null)
            {
                return SindriErrors.InvalidInput.With($"fact:{name} is not JSON: {broken}");
            }

            nodes[name] = node;
        }

        if (Report(scale, nodes["lensReport"]) is not { } report)
        {
            return SindriErrors.InvalidInput.With("fact:lensReport is not a report naming its lens, with findings on the loop's scale or a notApplicable reason (fix: report {\"lens\": <id>, \"findings\": [...]})");
        }

        var lens = LoopJson.Str(report, "lens")!;   // why: Report checked it names a lens
        var queue = nodes["lensQueue"]?.DeepClone() as JsonArray ?? [];
        var rounds = nodes["lensRounds"]?.DeepClone() as JsonArray ?? [];
        if (queue.FirstOrDefault(q => Names(q, lens)) is not { } queued)
        {
            return SindriErrors.InvalidInput.With($"the report is for the lens '{lens}', which fact:lensQueue does not hold (fix: run the queue's next lens)");
        }

        _ = queue.Remove(queued);
        var round = RoundOf(rounds, lens);
        rounds.Add((JsonNode?)Entry(report, round, closes: queue.Count == 0));
        return new RoundLog(scale, rounds, queue, round, FindingsOf(rounds, round));
    }

    private static JsonObject Entry(JsonObject report, int round, bool closes)
    {
        var entry = (JsonObject)report.DeepClone();
        _ = entry.Remove("closes");   // the log's own marker: one the agent copied from an earlier entry must not move the boundary
        entry["round"] = round;
        if (closes)
        {
            entry["closes"] = true;
        }

        return entry;
    }

    // The report when it names its lens and either has findings, every one on the scale, or says why the lens does not apply.
    private static JsonObject? Report(LensScale scale, JsonNode? node)
    {
        if (node is not JsonObject { } report || LoopJson.Str(report, "lens") is not { Length: > 0 })
        {
            return null;
        }

        return report["findings"] is JsonArray findings
            ? findings.All(f => f is JsonObject && scale.Rank(LoopJson.Str(f, "severity")) >= 0) ? report : null
            : LoopJson.Str(report, "notApplicable") is { Length: > 0 } ? report : null;
    }

    private static bool Names(JsonNode? queued, string lens) =>
        queued is JsonValue value ? value.TryGetValue<string>(out var id) && string.Equals(id, lens, StringComparison.Ordinal) : string.Equals(LoopJson.Str(queued, "id"), lens, StringComparison.Ordinal);

    // A round continues while its last entry is open; a closed round (or no log) makes the next entry the first of a new one, and so does a lens the open round already logged
    // (a log whose intake kept an earlier round's entries without their 'closes').
    private static int RoundOf(JsonArray rounds, string lens)
    {
        if (rounds.LastOrDefault() is not JsonObject last)
        {
            return 1;
        }

        var round = IntOf(last, "round") ?? 1;
        var closed = last["closes"] is JsonValue closes && closes.TryGetValue<bool>(out var flag) && flag;
        var repeated = rounds.OfType<JsonObject>().Any(e => IntOf(e, "round") == round && string.Equals(LoopJson.Str(e, "lens"), lens, StringComparison.Ordinal));
        return closed || repeated ? round + 1 : round;
    }

    private static int? IntOf(JsonObject entry, string name) => entry[name] is JsonValue value && value.TryGetValue<int>(out var n) ? n : null;

    private static List<JsonObject> FindingsOf(JsonArray rounds, int round) => [.. rounds.OfType<JsonObject>()
        .Where(e => e["round"] is JsonValue value && value.TryGetValue<int>(out var n) && n == round)
        .SelectMany(static e => LoopJson.Objects(e, "findings").Select(f => With(f, LoopJson.Str(e, "lens"))))];

    private static JsonObject With(JsonObject finding, string? lens)
    {
        var copy = (JsonObject)finding.DeepClone();
        copy["lens"] = lens;
        return copy;
    }

    /// <summary>The round number as text for a message.</summary>
    /// <returns>The text.</returns>
    public override string ToString() => Round.ToString(CultureInfo.InvariantCulture);
}
