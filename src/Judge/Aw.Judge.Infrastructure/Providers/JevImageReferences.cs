using System.Text.Json.Nodes;
using Aw.Judge.Core.Images;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>Puts an image's reference in the state where the image belongs, so Jev replaces it with the picture.</summary>
internal static class JevImageReferences
{
    /// <summary>
    /// A string field that names an image (its path, or its path relative to the state's 'evidenceDir') becomes the image's reference,
    /// and 'images' lists every reference in order. A state that is not an object is wrapped as 'input'.
    /// </summary>
    /// <param name="state">The redacted, capped state; changed in place.</param>
    /// <param name="images">The images, in order.</param>
    /// <param name="references">Their references, in the same order.</param>
    /// <returns>The state to send.</returns>
    public static JsonNode Replace(JsonNode? state, IReadOnlyList<ImageAttachment> images, IReadOnlyList<string> references)
    {
        var root = state as JsonObject ?? new JsonObject { ["input"] = state };
        var directory = root["evidenceDir"] is JsonValue dir && dir.TryGetValue<string>(out var text) ? text.TrimEnd('/') : null;
        foreach (var key in root.Select(pair => pair.Key).ToList())
        {
            if (root[key] is JsonValue value && value.TryGetValue<string>(out var field) && IndexOf(images, field, directory) is var i and >= 0)
            {
                root[key] = references[i];
            }
        }

        root["images"] = new JsonArray([.. references.Select(reference => JsonValue.Create(reference))]);
        return root;
    }

    private static int IndexOf(IReadOnlyList<ImageAttachment> images, string field, string? directory)
    {
        for (var i = 0; i < images.Count; i++)
        {
            var path = images[i].Path;
            if (string.Equals(path, field, StringComparison.Ordinal) || (directory is not null && string.Equals(path, directory + "/" + field, StringComparison.Ordinal)))
            {
                return i;
            }
        }

        return -1;
    }
}
