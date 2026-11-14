using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>The words the run list shows: a status as one lowercase word and a trigger's kind.</summary>
public static class RunWords
{
    private const int Cut = 120;

    /// <summary>A run status as its word ('queued', 'running', 'waiting', 'succeeded', 'failed', 'cancelled').</summary>
    /// <param name="status">The status.</param>
    /// <returns>The word.</returns>
    public static string Of(WorkflowRunStatus status) => status switch
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
    public static string Kind(string trigger)
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

    /// <summary>The trigger as the run list says it (#3683): 'manual', 'schedule', 'cli' or 'agent'. A stored 'manual' trigger started by 'session:&lt;id&gt;' is 'agent', one started by 'cli' is 'cli'; any other kind comes back as stored.</summary>
    /// <param name="trigger">The trigger JSON object.</param>
    /// <returns>The word.</returns>
    public static string TriggerWord(string trigger)
    {
        var kind = Kind(trigger);
        if (!string.Equals(kind, "manual", StringComparison.Ordinal))
        {
            return kind;
        }

        try
        {
            var by = JsonNode.Parse(trigger) is JsonObject obj && obj["by"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
            return by switch
            {
                "cli" => "cli",
                not null when by.StartsWith("session:", StringComparison.Ordinal) => "agent",
                _ => "manual",
            };
        }
        catch (JsonException)   // boundary: the store only keeps objects; Kind already accepted this one
        {
            return kind;
        }
    }

    /// <summary>How long a run took in seconds; null when it has not ended.</summary>
    /// <param name="started">When it began.</param>
    /// <param name="ended">When it ended, or null.</param>
    /// <returns>The seconds, or null.</returns>
    public static double? Seconds(DateTimeOffset started, DateTimeOffset? ended) => ended is { } end ? (end - started).TotalSeconds : null;

    /// <summary>The one-line result of a run: the first line of its error when it failed or was cancelled, its outputs as compact JSON cut at 120 characters when it succeeded, otherwise null.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The line, or null.</returns>
    public static string? Result(WorkflowRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.Status is WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled)
        {
            return run.Error?.Split('\n', 2)[0].TrimEnd('\r');
        }

        if (run.Status != WorkflowRunStatus.Succeeded || run.Outputs is null)
        {
            return null;
        }

        string compact;
        try
        {
            compact = JsonNode.Parse(run.Outputs)?.ToJsonString() ?? "null";
        }
        catch (JsonException)   // boundary: outputs are stored as JSON text; text that is not JSON is shown as it is
        {
            compact = run.Outputs;
        }

        return compact.Length <= Cut ? compact : compact[..Cut] + "…";
    }
}
