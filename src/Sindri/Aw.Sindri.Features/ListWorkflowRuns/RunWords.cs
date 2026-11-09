using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.ListWorkflowRuns;

/// <summary>The words the run list shows: a status as one lowercase word and a trigger's kind.</summary>
internal static class RunWords
{
    /// <summary>A run status as its word ('queued', 'running', 'waiting', 'succeeded', 'failed', 'cancelled').</summary>
    /// <param name="status">The status.</param>
    /// <returns>The word.</returns>
    internal static string Of(WorkflowRunStatus status) => status switch
    {
        WorkflowRunStatus.Queued => "queued",
        WorkflowRunStatus.Running => "running",
        WorkflowRunStatus.Waiting => "waiting",
        WorkflowRunStatus.Succeeded => "succeeded",
        WorkflowRunStatus.Failed => "failed",
        WorkflowRunStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    /// <summary>The 'kind' of a stored trigger ('manual', 'schedule'); 'unknown' when it has none.</summary>
    /// <param name="trigger">The trigger JSON object.</param>
    /// <returns>The kind.</returns>
    internal static string Kind(string trigger)
    {
        try
        {
            return JsonNode.Parse(trigger) is JsonObject obj && obj["kind"] is JsonValue kind && kind.TryGetValue<string>(out var text) ? text : "unknown";
        }
        catch (JsonException)   // boundary: the store only keeps objects, so this is a row written some other way; the list still shows it
        {
            return "unknown";
        }
    }
}
