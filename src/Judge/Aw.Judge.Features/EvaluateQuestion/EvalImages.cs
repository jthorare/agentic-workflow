using System.Text.Json.Nodes;
using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Features.EvaluateQuestion;

/// <summary>A stored input's images, read again: the row keeps each image's path and sha256, so a replay can tell the picture it judged from the one on disk now.</summary>
internal static class EvalImages
{
    /// <summary>The answer reason of a case whose image is gone or changed: skipped, not a failure.</summary>
    internal const string Changed = "image-changed";

    /// <summary>Re-reads each image the stored input names.</summary>
    /// <param name="stored">The stored input.</param>
    /// <param name="reader">Reads the files.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The images as they are now, and whether any is gone, has another sha256 or is not in the stored read form; no images for an input without any.</returns>
    internal static async ValueTask<(IReadOnlyList<ImageAttachment> Images, bool Changed)> Reread(JsonNode? stored, IImageReader? reader, CancellationToken cancellationToken)
    {
        if (stored is not JsonObject input || input["images"] is not JsonArray list || list.Count == 0)
        {
            return ([], false);
        }

        // A row that names images but cannot be checked against them (no reader, or an entry that is not the read form, such as a bare path) is not replayed as text.
        if (reader is null)
        {
            return ([], true);
        }

        List<ImageAttachment> images = [];
        foreach (var item in list)
        {
            if (item is not JsonObject fields || Stored(fields) is not { } entry)
            {
                return ([], true);
            }

            var read = await reader.Read([entry.Path], root: null, cancellationToken).ConfigureAwait(false);
            if (read.Problem is not null || !string.Equals(read.Images[0].Sha256, entry.Sha256, StringComparison.Ordinal))
            {
                return ([], true);
            }

            images.Add(read.Images[0]);
        }

        return (images, false);
    }

    private static (string Path, string Sha256)? Stored(JsonObject item) =>
        item["path"] is JsonValue path && path.TryGetValue<string>(out var file) && item["sha256"] is JsonValue sha && sha.TryGetValue<string>(out var expected)
            ? (file, expected)
            : null;

    /// <summary>The input a provider is shown: the stored read form of the images becomes the paths, and what was dropped is left out.</summary>
    /// <param name="input">The (variant of the) stored input.</param>
    /// <param name="images">The images as they are now.</param>
    /// <returns>The input.</returns>
    internal static JsonNode? ForProvider(JsonNode? input, IReadOnlyList<ImageAttachment> images)
    {
        if (input is not JsonObject obj || obj["images"] is null)
        {
            return input;
        }

        var copy = (JsonObject)obj.DeepClone();
        _ = copy.Remove("imagesDropped");
        _ = copy.Remove("images");
        if (images.Count > 0)
        {
            copy["images"] = new JsonArray([.. images.Select(image => (JsonNode?)JsonValue.Create(image.Path))]);
        }

        return copy;
    }
}
