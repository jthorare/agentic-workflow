using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Evaluation;

namespace Aw.Judge.Core.Decisions;

/// <summary>What a decision row's stored input says about its images: the read form of each ('images') and what a best-effort call dropped ('imagesDropped').</summary>
internal static class StoredImages
{
    /// <summary>One stored image: where it was, its hash and its size.</summary>
    /// <param name="Path">The path it was read from.</param>
    /// <param name="Sha256">The hash of its bytes.</param>
    /// <param name="Bytes">Its size in bytes.</param>
    /// <param name="Width">Its width.</param>
    /// <param name="Height">Its height.</param>
    internal sealed record Image(string Path, string Sha256, long Bytes, int Width, int Height);

    /// <summary>The images in read form; empty for a row without images, or one that kept the paths as given (a refused call).</summary>
    /// <param name="decision">The recorded decision.</param>
    /// <returns>The images.</returns>
    internal static IReadOnlyList<Image> Of(DecisionRecord decision)
    {
        if (Parse(decision) is not { } input || input["images"] is not JsonArray list)
        {
            return [];
        }

        return [.. list.OfType<JsonObject>().Select(Read).OfType<Image>()];
    }

    /// <summary>What the call dropped, one line each, without the 'jev images dropped: ' prefix.</summary>
    /// <param name="decision">The recorded decision.</param>
    /// <returns>The lines.</returns>
    internal static IReadOnlyList<string> Dropped(DecisionRecord decision) =>
        Parse(decision)?["imagesDropped"] is JsonArray list
            ? [.. list.Select(item => item is JsonValue value && value.TryGetValue<string>(out var text) ? text : null).OfType<string>()]
            : [];

    private static JsonObject? Parse(DecisionRecord decision) =>
        decision.Details is { } details && InputJson.TryParse(details.InputJson, out var node) ? node as JsonObject : null;

    private static Image? Read(JsonObject node) =>
        Text(node, "path") is { } path && Text(node, "sha256") is { } sha && Number<long>(node, "bytes") is { } size && Number<int>(node, "width") is { } w && Number<int>(node, "height") is { } h
            ? new Image(path, sha, size, w, h)
            : null;

    private static string? Text(JsonObject node, string name) =>
        node[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    private static T? Number<T>(JsonObject node, string name)
        where T : struct =>
        node[name] is JsonValue value && value.TryGetValue<T>(out var number) ? number : null;
}
