using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Decisions;

/// <summary>The 'jev skipped:' and 'jev images dropped:' lines 'judge why' prints: why Jev was passed over for a call with images, or what a best-effort call dropped, in words.</summary>
public static class JevSkipNotes
{
    /// <summary>The lines for a decision: one per Jev skip that stored a code; none for a decision without one.</summary>
    /// <param name="decision">The recorded decision.</param>
    /// <returns>Lines like 'jev skipped: Jev said the images were too large (413); asked claude-cli' or 'jev images dropped: image 1 is 200x120, under Jev's 280 px minimum; asked jev as text'.</returns>
    public static IReadOnlyList<string> For(DecisionRecord decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var next = string.Equals(decision.Provider, EvaluationReasons.NoProvider, StringComparison.Ordinal) ? "" : $"; asked {decision.Provider}";
        return [.. decision.Skipped
            .Where(skip => string.Equals(skip.Provider, ProviderNames.Name(ProviderName.Jev), StringComparison.Ordinal) && Words(skip.Code) is not null)
            .Select(skip => $"jev skipped: {Words(skip.Code)}{next}"),
            .. StoredImages.Dropped(decision).Select(line => $"jev images dropped: {line}")];
    }

    private static string? Words(string? code) => code switch
    {
        ProviderUnavailable.ImagesOff => "image sending to Jev is off (judge config set providers jev-images true)",
        ProviderUnavailable.ImageOutOfLimits => "an image is outside what Jev takes (280 to 1568 px a side, 78,400 to 2,458,624 pixels)",
        ProviderUnavailable.ImageTooLarge => "Jev said the images were too large (413)",
        EvaluationReasons.NoReasons => "jev answered with a problem but no reasons",
        _ => null,
    };
}
