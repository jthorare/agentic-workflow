using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>Jev for a model-pick call: its answers pass through unchanged, and the reason one is not usable is kept for the rules fallback.</summary>
/// <param name="inner">The Jev provider.</param>
/// <param name="threshold">The confidence below which an answer is not used.</param>
/// <param name="why">Where the reason goes.</param>
internal sealed class ModelPickJev(IModelProvider inner, double threshold, ModelPickWhy why) : IModelProvider
{
    /// <inheritdoc />
    public ProviderName Name => inner.Name;

    /// <inheritdoc />
    public IReadOnlySet<ContentClass> Classes => inner.Classes;

    /// <inheritdoc />
    public async ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);
        var result = await inner.Decide(question, input, budget, cancellationToken).ConfigureAwait(false);
        why.Reason = Reason(question, result);
        return result;
    }

    private string? Reason(QuestionRef question, ProviderResult result) => result switch
    {
        ProviderDecided decided when !question.Outputs.Contains(decided.Decision, StringComparer.Ordinal) => ModelPickWhy.UnknownKey,
        ProviderDecided decided when decided.Confidence < threshold => EvaluationReasons.BelowThreshold,
        ProviderDecided => null,
        ProviderUnavailable { ReasonCode: ProviderUnavailable.Timeout } => ModelPickWhy.InputRejected,
        ProviderErrored { ReasonCode: EvaluationReasons.OutOfEnum } => ModelPickWhy.UnknownKey,
        ProviderErrored errored when errored.ReasonCode.StartsWith("http-4", StringComparison.Ordinal) => ModelPickWhy.InputRejected,
        _ => result.ReasonCode,
    };
}
