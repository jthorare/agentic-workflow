using System.Globalization;

namespace Aw.Judge.Core.Decisions;

/// <summary>The image lines 'judge why' prints for a decision whose call had images: how many, and each one's path, size and hash.</summary>
public static class ImageNotes
{
    /// <summary>The lines for a decision: none for a call without images.</summary>
    /// <param name="decision">The recorded decision.</param>
    /// <returns>Lines like 'images: 1' and '  1. /home/u/r/home.png  1512x982  403 KB  sha256 3fa9c1d2e8b0'.</returns>
    public static IReadOnlyList<string> For(DecisionRecord decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var images = StoredImages.Of(decision);
        if (images.Count == 0)
        {
            return [];
        }

        return
        [
            string.Create(CultureInfo.InvariantCulture, $"images: {images.Count}"),
            .. images.Select((image, i) => string.Create(
                CultureInfo.InvariantCulture,
                $"  {i + 1}. {image.Path}  {image.Width}x{image.Height}  {Size(image.Bytes)}  sha256 {image.Sha256[..Math.Min(12, image.Sha256.Length)]}{Sent(decision, i)}")),
        ];
    }

    // '  -> sent to jev as 1568x1018' for an image Jev was sent smaller than it was read.
    private static string Sent(DecisionRecord decision, int index) =>
        string.Equals(decision.Provider, "jev", StringComparison.Ordinal) && decision.Details?.SentImages is { } sent && index < sent.Count && sent[index] is { } size
            ? string.Create(CultureInfo.InvariantCulture, $"  -> sent to jev as {size.Width}x{size.Height}")
            : "";

    private static string Size(long bytes) =>
        bytes >= 1024 * 1024 ? string.Create(CultureInfo.InvariantCulture, $"{bytes / 1048576d:0.#} MB") : string.Create(CultureInfo.InvariantCulture, $"{Math.Max(1, bytes / 1024)} KB");
}
