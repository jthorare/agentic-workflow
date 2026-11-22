using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>
/// What a run's result holds in place of a value over <see cref="NodeRunLimits.PayloadBytes"/> (#3807): {"truncated":true,"bytes":N}, for one result node or for the whole result.
/// A node's outputs are always an object or a list, so a boolean 'truncated' can't be one.
/// </summary>
public static class RunResultMarker
{
    /// <summary>The marker for a value of <paramref name="bytes"/> UTF-8 bytes.</summary>
    /// <param name="bytes">The value's size, or null when the store no longer knows it.</param>
    /// <returns>The marker.</returns>
    public static JsonObject Of(long? bytes) => throw new NotSupportedException();

    /// <summary>True when <paramref name="node"/> is a marker.</summary>
    /// <param name="node">A result or one node's slot in it.</param>
    /// <returns>Whether it is the marker.</returns>
    public static bool IsMarker(JsonNode? node) => throw new NotSupportedException();

    /// <summary>Why a reader can't use a run's result: the whole result or one node's slot in it is a marker.</summary>
    /// <param name="run">The run the result is of.</param>
    /// <param name="outputs">The run's outputs.</param>
    /// <returns>The refusal text, or null when the result holds no marker.</returns>
    public static string? Refusal(WorkflowRunId run, JsonNode? outputs) => throw new NotSupportedException();
}
