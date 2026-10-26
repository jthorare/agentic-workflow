using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Questions;

/// <summary>One judge question: what it may answer, how to ask a model, and its rules.</summary>
/// <typeparam name="TInput">
/// The question's input record. The evaluator reads it from the raw JSON through the package's AwJson options, so the
/// record must be in the package's JSON context; required and non-nullable members give the schema checks, unknown fields
/// are dropped, and ids are plain strings (convert them to typed ids after).
/// </typeparam>
public sealed record QuestionDefinition<TInput>
    where TInput : class
{
    /// <summary>The question name, e.g. 'wake-gate'.</summary>
    public required string Name { get; init; }

    /// <summary>The allowed decisions.</summary>
    public required IReadOnlyList<string> Outputs { get; init; }

    /// <summary>Builds the model prompt for an input.</summary>
    public required Func<TInput, string> Prompt { get; init; }

    /// <summary>The default confidence threshold (config may override it).</summary>
    public required double Threshold { get; init; }

    /// <summary>The input's content class (routes the chain).</summary>
    public required ContentClass ContentClass { get; init; }

    /// <summary>How long one provider may take.</summary>
    public required TimeSpan TimeBudget { get; init; }

    /// <summary>
    /// A check the input record's shape can't express (a required text that is blank): a non-null problem escalates the
    /// call as 'invalid-input', like input that doesn't deserialize.
    /// </summary>
    public Func<TInput, string?>? InputProblem { get; init; }

    /// <summary>Rules run before any model: a non-null answer settles it ('pre-rule', confidence 1).</summary>
    public Func<TInput, string?>? PreRules { get; init; }

    /// <summary>A cheap rule answer used only when a model answered below threshold; a throw counts as no opinion.</summary>
    public Func<TInput, string?>? FallbackRules { get; init; }

    /// <summary>Response fields beyond the decision, as JSON Schema fragments (chosenIndex, reasons); only these ride back to the caller.</summary>
    public IReadOnlyDictionary<string, JsonObject>? ExtraProperties { get; init; }

    /// <summary>
    /// Filters and caps a validated input before any provider sees it (the pack filters of model-pick); the result is what the
    /// prompt, the providers and the digest see. Null leaves the input as it is.
    /// </summary>
    public Func<TInput, IInputRedactor, TInput>? Sanitize { get; init; }

    /// <summary>
    /// What the decision row stores in place of the input, for a question whose input holds text that must not be kept (model-pick's
    /// files). A question that sets it never stores the caller's raw input, not even when the call escalates before it validates.
    /// </summary>
    public Func<TInput, JsonNode?>? StoredForm { get; init; }

    /// <summary>The cap on the state and the instructions Jev is sent; null keeps the stored input's cap.</summary>
    public int? InputCapChars { get; init; }

    /// <summary>What each output means.</summary>
    public IReadOnlyDictionary<string, string>? Criteria { get; init; }

    /// <summary>Whether the answer must state a confidence (see <see cref="QuestionRef.AskConfidence"/>); only choose sets it.</summary>
    public bool AskConfidence { get; init; }

    /// <summary>Whether the outcome carries the decision id (escalations) and the providers' probabilities; only choose sets it, so the hook questions' envelopes stay as they are.</summary>
    public bool ReportsTrace { get; init; }

    /// <summary>The outputs that only count with at least one non-blank string under 'reasons' (a bare classification of them explains nothing); a provider's answer without is skipped like a failed one.</summary>
    public IReadOnlySet<string>? ReasonedOutputs { get; init; }

    /// <summary>Whether <paramref name="decided"/> is one of the <see cref="ReasonedOutputs"/> and carries no reason.</summary>
    /// <param name="decided">A provider's answer.</param>
    /// <returns>True when it needs reasons and has none.</returns>
    public bool LacksReasons(ProviderDecided decided)
    {
        ArgumentNullException.ThrowIfNull(decided);
        return ReasonedOutputs?.Contains(decided.Decision) == true
            && !(decided.Extra?[ResolutionCheckQuestion.Reasons] is JsonArray reasons
                && reasons.Any(static r => r is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)));
    }

    /// <summary>What a provider sees of this question for one input.</summary>
    /// <param name="input">The validated input.</param>
    /// <returns>The reference.</returns>
    public QuestionRef ToRef(TInput input) => new()
    {
        Name = Name,
        Outputs = Outputs,
        Prompt = Prompt(input),
        ContentClass = ContentClass,
        ExtraProperties = ExtraProperties,
        Criteria = Criteria,
        AskConfidence = AskConfidence,
        InputCapChars = InputCapChars,
    };
}
