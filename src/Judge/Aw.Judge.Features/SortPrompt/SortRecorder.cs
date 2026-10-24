using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.PromptSort;
using Aw.Judge.Core.Providers;
using Aw.Kernel.Ids;

namespace Aw.Judge.Features.SortPrompt;

/// <summary>
/// What the sorter writes down: one 'prompt-sort' decision per prompt (so the telemetry and eval see it like any other
/// decision) with its detail row, then the axes and the run. The decision's value is the complexity level and its
/// confidence is the share of the eleven axes Jev decided. Everything stored is redacted first, then capped; a Jev error
/// or timeout is also a failure row, which 'judge health' counts.
/// </summary>
internal sealed class SortRecorder(IDecisionLog log, IPromptSortStore sorts, IInputRedactor redactor, IIdGenerator ids, TimeProvider clock, JsonSerializerOptions json)
{
    private const string Question = "prompt-sort";

    /// <summary>The prompt with its secrets removed, cut to what Jev gets.</summary>
    internal string Redact(string prompt)
    {
        var redacted = redactor.Redact(JsonValue.Create(prompt));
        var text = redacted is JsonValue value && value.TryGetValue<string>(out var s) ? s : "";
        return InputCap.Truncate(text, PromptSorter.StateCap);
    }

    /// <summary>Records the sort; best effort, the answer stands whatever happens here.</summary>
    internal async ValueTask Record(SortRecording sorted, CancellationToken cancellationToken)
    {
        var id = DecisionId.From(ids.NewId().ToString());
        var now = clock.GetUtcNow();
        var written = await log.Record(Decision(id, now, sorted), cancellationToken).ConfigureAwait(false);
        if (written.IsSuccess)
        {
            _ = await sorts.Save(Rows(id, sorted), cancellationToken).ConfigureAwait(false);
        }

        if (sorted.Outcome.Failure is { } failure)
        {
            _ = await log.RecordFailure(new FailureRecord(now, Question, ProviderNames.Name(ProviderName.Jev), failure), cancellationToken).ConfigureAwait(false);
        }
    }

    private DecisionRecord Decision(DecisionId id, DateTimeOffset now, SortRecording sorted)
    {
        var outcome = sorted.Outcome;
        var overrode = outcome.Axes.Any(static a => string.Equals(a.Status, "overrode", StringComparison.Ordinal));
        var probabilities = outcome.Axes.Where(static a => a.Probability is not null).ToDictionary(static a => a.Axis, static a => a.Probability!.Value, StringComparer.Ordinal);   // why: filtered to the non-null ones
        return new DecisionRecord
        {
            Id = id,
            Ts = now,
            Question = Question,
            ContentClass = ContentClasses.Name(ContentClass.Brief),
            Provider = ProviderNames.Name(outcome.JudgeDecided > 0 ? ProviderName.Jev : ProviderName.Rules),
            Decision = outcome.Values.Complexity,
            Confidence = (double)outcome.JudgeDecided / PromptSorter.Axes.Count,
            ReasonCode = string.Equals(outcome.Mode, "blend", StringComparison.Ordinal) ? "sorted" : $"heuristic-only:{outcome.Reason}",
            LatencyMs = (long)sorted.Latency.TotalMilliseconds,
            InputDigest = InputJson.Digest(new JsonObject { ["prompt"] = sorted.Prompt }, json),
            ChainPosition = 0,
            Skipped = Skipped(outcome),
            Outcome = DecisionOutcome.Decided,
            Details = new DecisionDetails
            {
                // The prompt was redacted before it was cut; redacting the object again also covers a key.
                InputJson = InputCap.Write(redactor.Redact(new JsonObject { ["prompt"] = sorted.Sent }), InputCap.MaxChars, json),
                Probabilities = probabilities.Count == 0 ? null : probabilities,
                Agreement = outcome.JudgeDecided == 0 ? Agreement.Undecided : overrode ? Agreement.Overrode : Agreement.Agreed,
                SessionId = sorted.Session,
            },
        };
    }

    private static List<SkippedProvider> Skipped(SortOutcome outcome)
    {
        if (string.Equals(outcome.Mode, "blend", StringComparison.Ordinal) || string.Equals(outcome.Reason, "no-jev", StringComparison.Ordinal) || string.Equals(outcome.Reason, "all-undecided", StringComparison.Ordinal))
        {
            return [];
        }

        var reason = outcome.Failure is null ? SkipReason.Unavailable : string.Equals(outcome.Failure, ProviderUnavailable.Timeout, StringComparison.Ordinal) ? SkipReason.Timeout : SkipReason.Failed;
        return [new SkippedProvider(ProviderNames.Name(ProviderName.Jev), reason)];
    }

    private static PromptSortRecord Rows(DecisionId id, SortRecording sorted) =>
        new(id, sorted.Outcome.Axes, new SortRun(sorted.Outcome.Mode, sorted.Outcome.Reason, sorted.Plan.WouldFire, sorted.Plan.Fired, sorted.Plan.Suppressed));
}
