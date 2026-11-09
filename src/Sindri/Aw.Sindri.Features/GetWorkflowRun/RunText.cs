using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.GetWorkflowRun;

/// <summary>The words and JSON values the run read shows: a status as one lowercase word and a stored JSON text as a value.</summary>
internal static class RunText
{
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

    internal static string Of(NodeRunStatus status) => status switch
    {
        NodeRunStatus.Running => "running",
        NodeRunStatus.Waiting => "waiting",
        NodeRunStatus.Succeeded => "succeeded",
        NodeRunStatus.Failed => "failed",
        NodeRunStatus.Skipped => "skipped",
        NodeRunStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    /// <summary>Stored JSON text as a value. Text the store cut short is not valid JSON, so it comes back as a string.</summary>
    /// <param name="text">The stored text, or null.</param>
    /// <returns>The value, or null when there was no text.</returns>
    internal static JsonNode? Json(string? text)
    {
        if (text is null)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)   // boundary: a payload cut at the size limit stops mid-value
        {
            return JsonValue.Create(text);
        }
    }
}
