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
    public void Skip(ProviderName provider, SkipReason reason) => _skipped.Add(new SkippedProvider(ProviderNames.Name(provider), reason));

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
