using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;

namespace Aw.Judge.Core.Evaluation;

/// <summary>
/// Asks one question: a disabled question or an invalid input escalates, a pre-rule settles, else the chain is walked
/// until a provider decides above threshold or a fallback rule settles a below-threshold answer. The rules are regexes
/// with a match timeout: a timeout (a huge input) is a 'rule-timeout' failure row, and escalates the call when it hits
/// the input check, a pre-rule or the prompt (a fallback that times out has no opinion); any other throw from a rule is
/// a bug and escapes. Every evaluation writes one decision row; storage failures never change the outcome.
/// </summary>
/// <param name="log">Where decisions and failures go.</param>
/// <param name="redactor">Strips secrets from the stored input.</param>
/// <param name="ids">Makes the decision id.</param>
/// <param name="clock">Timestamps and latencies.</param>
/// <param name="json">The package's AwJson options, for the digest and the stored input.</param>
public sealed partial class QuestionEvaluator(IDecisionLog log, IInputRedactor redactor, IIdGenerator ids, TimeProvider clock, JsonSerializerOptions json)
{
    /// <summary>Evaluates a question against a raw input.</summary>
    /// <typeparam name="TInput">The question's validated input.</typeparam>
    /// <param name="question">The question.</param>
    /// <param name="rawInput">The input text as read (stdin); anything that doesn't deserialize into the question's input record (syntax, a repeated key, a lone-surrogate escape, or shape) escalates as 'invalid-input'.</param>
    /// <param name="context">The providers, the chain and the question's config.</param>
    /// <param name="cancellationToken">Cancels the provider calls and the writes.</param>
    /// <returns>The decision, or an escalation.</returns>
    public async ValueTask<EvaluationOutcome> Evaluate<TInput>(
        QuestionDefinition<TInput> question, string rawInput, EvaluationContext context, CancellationToken cancellationToken)
        where TInput : class
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rawInput);
        var (rawNode, wellFormed) = RawNode(rawInput);
        var run = new EvaluationRun(DecisionId.From(ids.NewId().ToString()), clock.GetUtcNow(), rawNode) { SessionId = context.SessionId };
        var settings = context.Override;
        if (settings is { Enabled: false })
        {
            return await Escalate(question, run, EvaluationReasons.QuestionDisabled, DecisionOutcome.Escalated, cancellationToken).ConfigureAwait(false);
        }

        var (input, escalation) = wellFormed ? Validated(question, rawInput) : (null, EvaluationReasons.InvalidInput);
        if (input is null)
        {
            return escalation == EvaluationReasons.RuleTimeout
                ? await RuleTimedOut(question, run, cancellationToken).ConfigureAwait(false)
                : await Escalate(question, run, EvaluationReasons.InvalidInput, DecisionOutcome.Escalated, cancellationToken).ConfigureAwait(false);
        }

        // The input the prompt, the providers and the digest see: filtered and capped first when the question says how.
        var prepared = question.Sanitize is { } sanitize ? sanitize(input, redactor.Redact) : input;
        run.StoredInput = question.StoredForm?.Invoke(prepared);
        run.Input = JsonNode.Parse(AwJson.Serialize(prepared, json));
        run.Threshold = settings?.Threshold ?? question.Threshold;
        return await PreRuleOrWalk(question, prepared, context, run, cancellationToken).ConfigureAwait(false);
    }

    // The input record, or why the call escalates: it doesn't deserialize or fails the question's check ('invalid-input'),
    // or the check timed out ('rule-timeout').
    private (TInput? Input, string? Escalation) Validated<TInput>(QuestionDefinition<TInput> question, string rawInput)
        where TInput : class
    {
        var parsed = AwJson.Deserialize<TInput>(Encoding.UTF8.GetBytes(rawInput), json);
        if (parsed.IsError)
        {
            return (null, EvaluationReasons.InvalidInput);
        }

        if (TimedOut(() => question.InputProblem?.Invoke(parsed.Value), out var problem))
        {
            return (null, EvaluationReasons.RuleTimeout);
        }

        return problem is null ? (parsed.Value, null) : (null, EvaluationReasons.InvalidInput);
    }

    private async ValueTask<EvaluationOutcome> PreRuleOrWalk<TInput>(
        QuestionDefinition<TInput> question, TInput input, EvaluationContext context, EvaluationRun run, CancellationToken cancellationToken)
        where TInput : class
    {
        if (TimedOut(() => question.PreRules?.Invoke(input), out var preRule))
        {
            return await RuleTimedOut(question, run, cancellationToken).ConfigureAwait(false);
        }

        if (preRule is not null)
        {
            var settlement = Rule(preRule, EvaluationReasons.PreRule, latencyMs: 0, chainPosition: 0);
            return await Settle(question, run, settlement, extra: null, cancellationToken).ConfigureAwait(false);
        }

        var candidates = ChainRouting.Candidates(context.Chain, question.ContentClass, context.Providers, context.Override?.Providers);
        return await Walk(question, input, candidates, run, cancellationToken).ConfigureAwait(false);
    }

    // What escalation rows digest and store: the parsed JSON, or the text itself as a JSON string when it isn't JSON
    // that can be read and written back (a syntax error, a repeated key, a lone-surrogate escape). Text that isn't
    // escalates as 'invalid-input'.
    private static (JsonNode? Node, bool WellFormed) RawNode(string rawInput) =>
        InputJson.TryParse(rawInput, out var node) ? (node, true) : (JsonValue.Create(rawInput), false);

    private static Settlement Rule(string decision, string reasonCode, long latencyMs, int chainPosition) => new()
    {
        Outcome = DecisionOutcome.Decided,
        Decision = decision,
        Confidence = 1,
        Provider = ProviderName.Rules,
        ReasonCode = reasonCode,
        LatencyMs = latencyMs,
        ChainPosition = chainPosition,
    };

    // A rule's regex hit its match timeout: expected on a huge input, so it's answered, not thrown. Anything else escapes.
    private static bool TimedOut<T>(Func<T> rule, out T value)
    {
        try
        {
            value = rule();
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            // why: the caller reads value only when this returns false; there is no value on a timeout.
            value = default!;
            return true;
        }
    }

    // A rule that couldn't run leaves nothing safe to decide: a failure row, and the call escalates as failed.
    private async ValueTask<EvaluationOutcome> RuleTimedOut<TInput>(QuestionDefinition<TInput> question, EvaluationRun run, CancellationToken cancellationToken)
        where TInput : class
    {
        await Fail(question.Name, ProviderName.Rules, EvaluationReasons.RuleTimeout, cancellationToken).ConfigureAwait(false);
        return await Escalate(question, run, EvaluationReasons.RuleTimeout, DecisionOutcome.Failed, cancellationToken).ConfigureAwait(false);
    }

    // Fallback rules are advisory: one that times out has no opinion (and leaves a failure row).
    private async ValueTask<string?> Fallback<TInput>(QuestionDefinition<TInput> question, TInput input, CancellationToken cancellationToken)
        where TInput : class
    {
        if (question.FallbackRules is not { } rules)
        {
            return null;
        }

        if (TimedOut(() => rules(input), out var opinion))
        {
            await Fail(question.Name, ProviderName.Rules, EvaluationReasons.RuleTimeout, cancellationToken).ConfigureAwait(false);
            return null;
        }

        return opinion;
    }

    private async ValueTask<EvaluationOutcome> Walk<TInput>(
        QuestionDefinition<TInput> question, TInput input, IReadOnlyList<IModelProvider> candidates, EvaluationRun run,
        CancellationToken cancellationToken)
        where TInput : class
    {
        if (TimedOut(() => question.ToRef(input), out var reference))
        {
            return await RuleTimedOut(question, run, cancellationToken).ConfigureAwait(false);
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var provider = candidates[i];
            var start = clock.GetTimestamp();
            var result = await provider.Decide(reference, run.Input, question.TimeBudget, cancellationToken).ConfigureAwait(false);
            var latencyMs = (long)clock.GetElapsedTime(start).TotalMilliseconds;
            var answer = new ProviderAnswer(provider.Name, i, latencyMs, result);
            var settlement = await Step(question, input, run, answer, cancellationToken).ConfigureAwait(false);
            if (settlement is not null)
            {
                return await Settle(question, run, settlement, (result as ProviderDecided)?.Extra, cancellationToken).ConfigureAwait(false);
            }
        }

        var outcome = run.HadRealFailure ? DecisionOutcome.Failed : DecisionOutcome.Escalated;
        return await Escalate(question, run, run.LastReason, outcome, cancellationToken).ConfigureAwait(false);
    }

    // One provider's answer: a settlement when it ends the walk, null to try the next provider.
    private async ValueTask<Settlement?> Step<TInput>(
        QuestionDefinition<TInput> question, TInput input, EvaluationRun run, ProviderAnswer at, CancellationToken cancellationToken)
        where TInput : class
    {
        var result = at.Result;
        var verdict = Judged(question, run, result);
        switch (verdict)
        {
            case StepVerdict.Unavailable:
                run.Skip(at.Name, SkipReason.Unavailable);
                return null;
            case StepVerdict.TimedOut:
                await Fail(question.Name, at.Name, ProviderUnavailable.Timeout, cancellationToken).ConfigureAwait(false);
                run.Skip(at.Name, SkipReason.Timeout);
                return null;
            case StepVerdict.Errored:
            case StepVerdict.OutOfEnum:
            case StepVerdict.NoReasons:
                await Fail(question.Name, at.Name, FailureCode(verdict, result), cancellationToken).ConfigureAwait(false);
                run.Skip(at.Name, SkipReason.Failed);
                return null;
            case StepVerdict.BelowThreshold:
                run.Undecided(at.Name, (ProviderDecided)result);
                return await Fallback(question, input, cancellationToken).ConfigureAwait(false) is { } fallback
                    ? Rule(fallback, EvaluationReasons.FallbackAfterUndecided, at.LatencyMs, at.Position) with
                    {
                        Probabilities = run.UndecidedProbabilities,
                        Agreement = Agreement.Undecided,
                    }
                    : null;
            case StepVerdict.Decided:
            default:
                return Decided(run, at, (ProviderDecided)result, await Fallback(question, input, cancellationToken).ConfigureAwait(false));
        }
    }

    private static StepVerdict Judged<TInput>(QuestionDefinition<TInput> question, EvaluationRun run, ProviderResult result)
        where TInput : class =>
        StepVerdicts.Of(result, question.Outputs, run.Threshold, lacksReasons: result is ProviderDecided decided && question.LacksReasons(decided));

    // The failure row's reason: the provider's own, or the engine's when the provider answered but the answer can't be used.
    private static string FailureCode(StepVerdict verdict, ProviderResult result)
    {
        if (verdict == StepVerdict.NoReasons)
        {
            return EvaluationReasons.NoReasons;
        }

        return result is ProviderDecided ? EvaluationReasons.OutOfEnum : result.ReasonCode;
    }

    // opinion: what the fallback rules would have said; telemetry only, never a change to the model's decision.
    private static Settlement Decided(EvaluationRun run, ProviderAnswer at, ProviderDecided answer, string? opinion)
    {
        Agreement? agreement = opinion is null ? null
            : string.Equals(opinion, answer.Decision, StringComparison.Ordinal) ? Agreement.Agreed : Agreement.Overrode;
        return new Settlement
        {
            Outcome = DecisionOutcome.Decided,
            Decision = answer.Decision,
            Confidence = answer.Confidence,
            Provider = at.Name,
            ReasonCode = answer.ReasonCode,
            LatencyMs = at.LatencyMs,
            ChainPosition = at.Position,
            Probabilities = answer.Probabilities ?? run.UndecidedProbabilities,
            DecidingProbabilities = answer.Probabilities,
            RulesOpinion = opinion,
            Agreement = agreement,
        };
    }
}
