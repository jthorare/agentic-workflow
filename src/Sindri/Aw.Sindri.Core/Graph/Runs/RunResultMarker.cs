using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>
/// What a run's result holds in place of a value over <see cref="NodeRunLimits.PayloadBytes"/> (#3807): {"truncated":true,"bytes":N}, for one result node or for the whole result.
/// A step may return its own top-level 'truncated' field (GitHub's tree API body has one, #4089), so only the marker's exact shape counts:
/// 'truncated' true, an optional whole-number 'bytes', and no other key.
/// </summary>
public static class RunResultMarker
{
    /// <summary>The marker for a value of <paramref name="bytes"/> UTF-8 bytes.</summary>
    /// <param name="bytes">The value's size, or null when the store no longer knows it.</param>
    /// <returns>The marker.</returns>
    public static JsonObject Of(long? bytes) => bytes is { } size ? new JsonObject { ["truncated"] = true, ["bytes"] = size } : new JsonObject { ["truncated"] = true };

    /// <summary>True when <paramref name="node"/> is a marker: exactly 'truncated' true and an optional 'bytes', so a step output with other keys never is.</summary>
    /// <param name="node">A result or one node's slot in it.</param>
    /// <returns>Whether it is the marker.</returns>
    public static bool IsMarker(JsonNode? node) =>
        node is JsonObject marker && marker.Count <= 2 && Flagged(marker) && (marker.Count == 1 || (marker["bytes"] is JsonValue size && size.TryGetValue<long>(out _)));

    /// <summary>Why a reader can't use a run's result: the whole result or one node's slot in it is a marker.</summary>
    /// <param name="run">The run the result is of.</param>
    /// <param name="outputs">The run's outputs.</param>
    /// <returns>The refusal text, or null when the result holds no marker.</returns>
    public static string? Refusal(WorkflowRunId run, JsonNode? outputs)
    {
        if (IsMarker(outputs))
        {
            return $"the result of run {run.Value}{Size(outputs)} passes the 64 KB a run's result keeps; name one smaller node with the workflow's 'result'";
        }

        return outputs is JsonObject slots && slots.FirstOrDefault(static s => IsMarker(s.Value)) is { Key: { } node, Value: var slot }
            ? $"the output of '{node}' in the result of run {run.Value}{Size(slot)} was cut at 64 KB; name a smaller node with the workflow's 'result'"
            : null;
    }

    private static string Size(JsonNode? marker) =>
        marker is JsonObject found && found["bytes"] is JsonValue value && value.TryGetValue<long>(out var bytes) ? $" ({bytes.ToString(CultureInfo.InvariantCulture)} bytes)" : string.Empty;

    private static bool Flagged(JsonObject marker) => marker["truncated"] is JsonValue flag && flag.TryGetValue<bool>(out var truncated) && truncated;
}
