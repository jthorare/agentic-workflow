using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Judge.Core.Evaluation;

/// <summary>
/// The 'images' and 'imagesBestEffort' fields any question's input may hold, as the caller gave them (nothing read yet). The question's own
/// record never sees them: unknown fields are dropped when it deserializes.
/// </summary>
/// <param name="Paths">The paths as given, in order.</param>
/// <param name="BestEffort">Whether a bad image is dropped instead of refusing the call.</param>
/// <param name="Problem">Why the field can't be read as a list of paths, or null.</param>
internal sealed record GenericImages(IReadOnlyList<string> Paths, bool BestEffort, string? Problem)
{
    /// <summary>The most images a call takes.</summary>
    public const int MaxImages = 4;

    /// <summary>The most bytes the images of one call take together.</summary>
    public const long MaxBytes = 24L * 1024 * 1024;

    /// <summary>Reads the fields from the raw input.</summary>
    /// <param name="raw">The input as JSON.</param>
    /// <returns>The fields, or null for an input with none or an empty list (today's call).</returns>
    public static GenericImages? From(JsonNode? raw)
    {
        if (raw is not JsonObject input || input["images"] is not { } field)
        {
            return null;
        }

        if (field is not JsonArray list)
        {
            return new GenericImages([], BestEffort: false, "images must be a list of absolute file paths");
        }

        if (list.Count == 0)
        {
            return null;
        }

        var paths = new List<string>(list.Count);
        foreach (var item in list)
        {
            if (item is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
            {
                return new GenericImages([], BestEffort: false, "images must be a list of absolute file paths");
            }

            paths.Add(value.GetValue<string>());
        }

        var bestEffort = input["imagesBestEffort"] is JsonValue flag && flag.GetValueKind() == JsonValueKind.True;
        return new GenericImages(paths, bestEffort, Problem: null);
    }
}
