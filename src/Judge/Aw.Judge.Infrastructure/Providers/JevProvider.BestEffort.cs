using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Images;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>A hook's call (imagesBestEffort): an image Jev can't take is dropped and Jev still answers, as text when none are left.</summary>
internal sealed partial class JevProvider
{
    private const string Minimum = "under Jev's 280 px minimum";

    private const string OutOfLimits = "an image is outside what Jev takes (280 to 1568 px a side, 78,400 to 2,458,624 pixels)";

    /// <inheritdoc />
    public ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken) =>
        question is { ImagesBestEffort: true, Images.Count: > 0 }
            ? DecideBestEffort(question, input, budget, cancellationToken)
            : DecideOnce(question, input, budget, cancellationToken);

    // Drops the images under Jev's minimum, asks with the rest, and when Jev can't take those either (out of limits, a 413, a 400 on an image call)
    // asks once more as text if at least half the budget is left; else Jev counts as errored. The answer carries what was dropped, so the rest of the chain runs as text.
    private async ValueTask<ProviderResult> DecideBestEffort(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var (kept, note) = Partition(question.Images);
        List<string> notes = note is null ? [] : [note];
        var result = await DecideOnce(JevImageDrops.With(question, kept), JevImageDrops.With(input, kept), budget, cancellationToken).ConfigureAwait(false);
        if (kept.Count > 0 && Retry(result) is { } cause)
        {
            var left = budget - Stopwatch.GetElapsedTime(started);
            if (left * 2 < budget)
            {
                // Not enough budget to ask again: Jev errored, as on a timeout, and the rest of the chain runs as text.
                notes.Add($"{cause}; not enough of the budget was left to ask jev again");
                result = Exhausted(result);
            }
            else
            {
                notes.Add($"{cause}; asked jev again as text");
                result = await DecideOnce(JevImageDrops.With(question, []), JevImageDrops.With(input, []), left, cancellationToken).ConfigureAwait(false);
            }
        }

        result = Aligned(result, question.Images, kept);
        return notes.Count == 0 ? result : result with { ImageNotes = notes };
    }

    // The images Jev can take, and one line for the rest ('image 1 is 200x120, under Jev's 280 px minimum; asked jev with images 2, 3'), null when none were dropped.
    private (List<ImageAttachment> Kept, string? Note) Partition(IReadOnlyList<ImageAttachment> images)
    {
        List<ImageAttachment> kept = [];
        List<string> why = [];
        List<int> numbers = [];
        for (var i = 0; i < images.Count; i++)
        {
            if (TooSmall(images[i]))
            {
                why.Add(string.Create(CultureInfo.InvariantCulture, $"image {i + 1} is {images[i].Width}x{images[i].Height}, {Minimum}"));
            }
            else if (Fitted([images[i]]) is null)
            {
                // No downscale makes it fit (4000x500: the short side would land under 280 px): this image goes, the rest stay on Jev.
                why.Add(string.Create(CultureInfo.InvariantCulture, $"image {i + 1} is {images[i].Width}x{images[i].Height}; fitting it under 1568 px leaves it under 280 px"));
            }
            else
            {
                kept.Add(images[i]);
                numbers.Add(i + 1);
            }
        }

        return (kept, why.Count == 0 ? null : $"{string.Join("; ", why)}; asked jev {(kept.Count == 0 ? "as text" : "with images " + string.Join(", ", numbers))}");
    }

    // SentImages names one size per READ image, by index: a dropped image holds its read size (it went nowhere), so only a scaled one differs.
    private static ProviderResult Aligned(ProviderResult result, IReadOnlyList<ImageAttachment> read, List<ImageAttachment> kept)
    {
        if (result is not ProviderDecided { SentImages: { } sent } decided || sent.Count != kept.Count || kept.Count == read.Count)
        {
            return result;
        }

        var next = 0;
        return decided with
        {
            SentImages = [.. read.Select(image => kept.Contains(image) ? sent[next++] : new ImageSize(image.Width, image.Height))],
        };
    }

    // The answer that stands when there is no budget to ask again: Jev's own, except that a 413 (told to the CLIs as 'image too large') is an errored 'http-413' for Jev's health.
    private static ProviderResult Exhausted(ProviderResult result) =>
        result is ProviderUnavailable unavailable && string.Equals(unavailable.ReasonCode, ProviderUnavailable.ImageTooLarge, StringComparison.Ordinal) ? new ProviderErrored(ProviderReasons.Http(413)) : result;

    // Why Jev refused the images it was sent, in words, or null when the answer isn't about them.
    private static string? Retry(ProviderResult result) => result switch
    {
        ProviderUnavailable { ReasonCode: ProviderUnavailable.ImageOutOfLimits } => OutOfLimits,
        ProviderUnavailable { ReasonCode: ProviderUnavailable.ImageTooLarge } => "Jev said the images were too large (413)",
        ProviderErrored { ReasonCode: "http-400" } => "Jev refused the request (400)",
        _ => null,
    };

    private static bool TooSmall(ImageAttachment image) =>
        image.Width < 280 || image.Height < 280 || (long)image.Width * image.Height < 78_400;
}
