using System.Text.Json.Nodes;
using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>The state of one evaluation as it walks the chain.</summary>
/// <param name="id">The decision id.</param>
/// <param name="ts">When the evaluation started.</param>
/// <param name="rawInput">The input as read, as JSON (an escalation before the input validates digests and stores it).</param>
internal sealed class EvaluationRun(DecisionId id, DateTimeOffset ts, JsonNode? rawInput)
{
    private readonly List<SkippedProvider> _skipped = [];

    /// <summary>The decision id.</summary>
    public DecisionId Id { get; } = id;

    /// <summary>When the evaluation started.</summary>
    public DateTimeOffset Ts { get; } = ts;

    /// <summary>The calling session, when known.</summary>
    public SessionId? SessionId { get; init; }

    /// <summary>The input as read.</summary>
    public JsonNode? RawInput { get; } = rawInput;

    /// <summary>The validated input as JSON (what providers see and decided rows digest).</summary>
    public JsonNode? Input { get; set; }

    /// <summary>What the row stores in place of the input, for a question that keeps no input text (<see cref="Questions.QuestionDefinition{TInput}.StoredForm"/>).</summary>
    public JsonNode? StoredInput { get; set; }

    /// <summary>Why an escalation happened, when it is more than its reason code (an image that couldn't be read); stored as the row's reason.</summary>
    public string? Note { get; set; }

    /// <summary>The input's 'images' and 'imagesBestEffort', or null for a call without images.</summary>
    public GenericImages? Generic { get; set; }

    /// <summary>The input's own images, once read and kept.</summary>
    public IReadOnlyList<Images.ImageAttachment> GenericRead { get; set; } = [];

    /// <summary>The causes of the images the evaluator dropped (best effort), without the 'judge: ' prefix.</summary>
    public List<string> DropCauses { get; } = [];

    /// <summary>What providers said they dropped on the way (Jev's limits, a 413 retried as text), complete lines.</summary>
    public List<string> ProviderDrops { get; } = [];

    /// <summary>The lines for stderr: empty (the shared empty list) when there are none, so an outcome without images equals today's.</summary>
    public IReadOnlyList<string> Lines
    {
        get
        {
            List<string> causes = [.. DropCauses];
            if (Refusal is { } refusal)
            {
                causes.Add(refusal);
            }

            return causes.Count == 0 ? [] : [.. causes.Select(cause => "judge: " + cause)];
        }
    }

    /// <summary>The cause the call was refused for, when its images failed a check and the call is strict.</summary>
    public string? Refusal { get; set; }

    /// <summary>The effective threshold.</summary>
    public double Threshold { get; set; }

    /// <summary>The providers passed over so far.</summary>
    public IReadOnlyList<SkippedProvider> Skipped => _skipped;

    /// <summary>'below-threshold' once a provider answered below threshold, else 'no-provider-decided'.</summary>
    public string LastReason { get; private set; } = EvaluationReasons.NoProviderDecided;

    /// <summary>The most recent below-threshold answer's probabilities.</summary>
    public IReadOnlyDictionary<string, double>? UndecidedProbabilities { get; private set; }

    /// <summary>Whether a provider really failed or timed out (an exhausted chain is then 'failed', not 'escalated').</summary>
    public bool HadRealFailure => _skipped.Exists(s => s.Reason is SkipReason.Failed or SkipReason.Timeout);

    /// <summary>Passes over a provider.</summary>
    /// <param name="provider">The provider.</param>
    /// <param name="reason">Why.</param>
    /// <param name="code">The finer reason to store with it, or null.</param>
    public void Skip(ProviderName provider, SkipReason reason, string? code = null) =>
        _skipped.Add(new SkippedProvider(ProviderNames.Name(provider), reason) { Code = code });

    /// <summary>Records a below-threshold answer; its probabilities replace earlier ones only when it has some.</summary>
    /// <param name="provider">The provider.</param>
    /// <param name="answer">The answer.</param>
    public void Undecided(ProviderName provider, ProviderDecided answer)
    {
        Skip(provider, SkipReason.BelowThreshold);
        LastReason = EvaluationReasons.BelowThreshold;
        UndecidedProbabilities = answer.Probabilities ?? UndecidedProbabilities;
    }
}
