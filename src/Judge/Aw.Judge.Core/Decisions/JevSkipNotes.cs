using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Decisions;

/// <summary>The 'jev skipped:' lines 'judge why' prints: why Jev was passed over for a call with images, in words, and who was asked next.</summary>
public static class JevSkipNotes
{
    /// <summary>The lines for a decision: one per image Jev was sent smaller than it was read, then one per Jev skip that stored a code; none for a decision without either.</summary>
    /// <param name="decision">The recorded decision.</param>
    /// <returns>Lines like 'jev skipped: Jev said the images were too large (413); asked claude-cli'.</returns>
    public static IReadOnlyList<string> For(DecisionRecord decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var next = string.Equals(decision.Provider, EvaluationReasons.NoProvider, StringComparison.Ordinal) ? "" : $"; asked {decision.Provider}";
        return [.. Scaled(decision), .. decision.Skipped
            .Where(skip => string.Equals(skip.Provider, ProviderNames.Name(ProviderName.Jev), StringComparison.Ordinal) && Words(skip.Code) is not null)
            .Select(skip => $"jev skipped: {Words(skip.Code)}{next}")];
    }

    // Jev was sent an image smaller than the one read: 'image 1 3024x1964 -> sent to jev as 1568x1018'.
    private static IEnumerable<string> Scaled(DecisionRecord decision)
    {
        if (decision.Details?.Images is not { } images || !string.Equals(decision.Provider, ProviderNames.Name(ProviderName.Jev), StringComparison.Ordinal))
        {
            yield break;
        }

        for (var i = 0; i < images.Sent.Count && i < images.Read.Count; i++)
        {
            if (images.Sent[i] != images.Read[i])
            {
                yield return string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"image {i + 1} {images.Read[i].Width}x{images.Read[i].Height} -> sent to jev as {images.Sent[i].Width}x{images.Sent[i].Height}");
            }
        }
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
