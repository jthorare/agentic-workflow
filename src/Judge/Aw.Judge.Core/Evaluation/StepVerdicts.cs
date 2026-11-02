using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>Classifies a provider's answer.</summary>
internal static class StepVerdicts
{
    /// <summary>The verdict for one answer.</summary>
    /// <param name="result">The answer.</param>
    /// <param name="outputs">The question's outputs.</param>
    /// <param name="threshold">The effective threshold.</param>
    /// <param name="lacksReasons">Whether an answer would need reasons it does not have.</param>
    /// <returns>The verdict.</returns>
    public static StepVerdict Of(ProviderResult result, IReadOnlyList<string> outputs, double threshold, bool lacksReasons) => result switch
    {
        ProviderUnavailable { ReasonCode: ProviderUnavailable.Timeout } => StepVerdict.TimedOut,
        ProviderUnavailable => StepVerdict.Unavailable,
        ProviderDecided decided when !outputs.Contains(decided.Decision, StringComparer.Ordinal) => StepVerdict.OutOfEnum,
        ProviderDecided decided when decided.Confidence < threshold => StepVerdict.BelowThreshold,
        ProviderDecided when lacksReasons => StepVerdict.NoReasons,
        ProviderDecided => StepVerdict.Decided,
        _ => StepVerdict.Errored,
    };
}
