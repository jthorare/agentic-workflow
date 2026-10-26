using System.Text.Json.Nodes;
using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;

namespace Aw.Judge.Core.Evaluation;

/// <summary>The evaluator's writes: decision rows and failure rows, all best effort.</summary>
public sealed partial class QuestionEvaluator
{
    private async ValueTask<EvaluationOutcome> Escalate<TInput>(
        QuestionDefinition<TInput> question, EvaluationRun run, string reasonCode, DecisionOutcome outcome, CancellationToken cancellationToken)
        where TInput : class
    {
        var settlement = new Settlement
        {
            Outcome = outcome,
            ReasonCode = reasonCode,
            ChainPosition = run.Skipped.Count,
            Probabilities = run.UndecidedProbabilities,
        };
        // Once the input validated, the row holds the record (its declared fields), as a decided row does; before that
        // (disabled, invalid) it holds what the caller sent.
        await Record(question, run, settlement, run.Input ?? run.RawInput, cancellationToken).ConfigureAwait(false);
        return question.ReportsTrace
            ? new EscalatedOutcome(reasonCode) { Id = run.Id, Probabilities = run.UndecidedProbabilities }
            : new EscalatedOutcome(reasonCode);
    }

    private async ValueTask<EvaluationOutcome> Settle<TInput>(
        QuestionDefinition<TInput> question, EvaluationRun run, Settlement settlement, JsonObject? extra, CancellationToken cancellationToken)
        where TInput : class
    {
        await Record(question, run, settlement with { Reasons = extra?[ResolutionCheckQuestion.Reasons] }, run.Input, cancellationToken).ConfigureAwait(false);
        // why: a settlement that decides always carries its decision (Rule and Decided set it).
        var decision = settlement.Decision!;
        // why: a settlement that decides always names who decided.
        var model = settlement.Provider!.Value;
        return new DecidedOutcome(decision, settlement.Confidence, model, settlement.ReasonCode, run.Id)
        {
            Extra = Declared(extra, question.ExtraProperties),
            Probabilities = question.ReportsTrace ? settlement.DecidingProbabilities : null,
        };
    }

    // Only the fields the question declares ride back to the caller; anything else a provider sent is dropped.
    private static JsonObject? Declared(JsonObject? extra, IReadOnlyDictionary<string, JsonObject>? declared)
    {
        if (extra is null || declared is null)
        {
            return null;
        }

        var kept = new JsonObject(extra.Where(pair => declared.ContainsKey(pair.Key)).Select(pair => KeyValuePair.Create(pair.Key, pair.Value?.DeepClone())));
        return kept.Count == 0 ? null : kept;
    }

    // Storage fails open: the outcome stands whatever happens here, and a failed write is itself a judge failure.
    private async ValueTask Record<TInput>(
        QuestionDefinition<TInput> question, EvaluationRun run, Settlement settlement, JsonNode? input, CancellationToken cancellationToken)
        where TInput : class
    {
        var decision = new DecisionRecord
        {
            Id = run.Id,
            Ts = run.Ts,
            Question = question.Name,
            ContentClass = ContentClasses.Name(question.ContentClass),
            Provider = settlement.ProviderColumn,
            Decision = settlement.Decision,
            Confidence = settlement.Confidence,
            ReasonCode = settlement.ReasonCode,
            LatencyMs = settlement.LatencyMs,
            InputDigest = InputJson.Digest(input, json),
            ChainPosition = settlement.ChainPosition,
            Skipped = [.. run.Skipped],
            Outcome = settlement.Outcome,
            Details = new DecisionDetails
            {
                // Redact the parsed values first, then cap, so a secret cut at the cap can't survive.
                InputJson = InputCap.Write(redactor.Redact(input), InputCap.MaxChars, json),
                Reasons = StoredReasons(settlement.Reasons),
                Probabilities = settlement.Probabilities,
                RulesOpinion = settlement.RulesOpinion,
                Agreement = settlement.Agreement,
                SessionId = run.SessionId,
            },
        };
        var written = await log.Record(decision, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            await Fail(question.Name, settlement.ProviderColumn, EvaluationReasons.DecisionWriteFailed, cancellationToken).ConfigureAwait(false);
        }
    }

    // The model's reasons, redacted then capped like the input; null when the answer carried none.
    private IReadOnlyList<string>? StoredReasons(JsonNode? reasons)
    {
        if (reasons is not JsonArray)
        {
            return null;
        }

        var capped = JsonNode.Parse(InputCap.Write(redactor.Redact(reasons), InputCap.MaxChars, json));
        return capped is JsonArray array
            ? [.. array.OfType<JsonValue>().Select(static v => v.TryGetValue<string>(out var text) ? text : null).OfType<string>()]
            : [capped?.ToJsonString() ?? ""];   // over the cap even with cut strings: the stub object, kept as one line
    }

    private ValueTask Fail(string question, ProviderName provider, string reasonCode, CancellationToken cancellationToken) =>
        Fail(question, ProviderNames.Name(provider), reasonCode, cancellationToken);

    // A failed failure write has nothing left to fall back to; the caller already fails open.
    private async ValueTask Fail(string question, string provider, string reasonCode, CancellationToken cancellationToken) =>
        _ = await log.RecordFailure(new FailureRecord(clock.GetUtcNow(), question, provider, reasonCode), cancellationToken).ConfigureAwait(false);
}
