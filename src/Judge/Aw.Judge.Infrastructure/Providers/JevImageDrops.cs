using System.Text.Json.Nodes;
using Aw.Judge.Core.Images;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>What a best-effort call sends Jev once some or all of its images are dropped: the same question and input, with the images taken out.</summary>
internal static class JevImageDrops
{
    /// <summary>The question with only <paramref name="kept"/> images (none: a plain text question).</summary>
    /// <param name="question">The question.</param>
    /// <param name="kept">The images that stay.</param>
    /// <returns>The question to ask.</returns>
    public static QuestionRef With(QuestionRef question, IReadOnlyList<ImageAttachment> kept) =>
        question with { Images = kept, ImagesBestEffort = false, ImageScope = kept.Count == 0 ? null : question.ImageScope };

    /// <summary>The input with its 'images' list cut to <paramref name="kept"/>, or without it when none stay (a text call's input is then the one it was without images).</summary>
    /// <param name="input">The input.</param>
    /// <param name="kept">The images that stay.</param>
    /// <returns>The input to send.</returns>
    public static JsonNode? With(JsonNode? input, IReadOnlyList<ImageAttachment> kept)
    {
        if (input is not JsonObject obj)
        {
            return input;
        }

        var copy = (JsonObject)obj.DeepClone();
        _ = copy.Remove("images");
        if (kept.Count > 0)
        {
            copy["images"] = new JsonArray([.. kept.Select(image => (JsonNode?)JsonValue.Create(image.Path))]);
        }

        return copy;
    }
}
