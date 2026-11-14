using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>One run of one workflow revision (table 'workflow_run').</summary>
/// <param name="Id">'&lt;slug&gt;@&lt;revision&gt;:&lt;occurrence&gt;'; also the Temporal workflow id.</param>
/// <param name="WorkflowId">The workflow.</param>
/// <param name="Revision">The revision the run walks.</param>
/// <param name="Trigger">What started it, as JSON ('{"kind":"manual"}').</param>
/// <param name="Status">Where it stands.</param>
/// <param name="Outputs">Its outputs as JSON, or null while none: when it succeeded, an object keyed by node id with each succeeded node's outputs (an array per item for a node that ran per item).</param>
/// <param name="Error">Why it failed, or null.</param>
/// <param name="ItemId">The item it works for, or null.</param>
/// <param name="StartedAt">When the run was recorded.</param>
/// <param name="EndedAt">When it reached a final status, or null.</param>
/// <param name="Mode">Live, or a test run that records every write instead of making it (spec 2026-11-12 §3.2).</param>
/// <param name="TestOptions">How a test ran (with model, with agents, the pinned nodes) as JSON; null for a live run.</param>
public sealed record WorkflowRun(
    WorkflowRunId Id,
    WorkflowId WorkflowId,
    int Revision,
    string Trigger,
    WorkflowRunStatus Status,
    string? Outputs,
    string? Error,
    ItemId? ItemId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    string Mode = WorkflowRunMode.Live,
    string? TestOptions = null)
{
    /// <summary>Why <paramref name="trigger"/> can't be a run's trigger: it must be a JSON object, since runs are found by its 'kind'.</summary>
    /// <param name="trigger">The trigger text.</param>
    /// <returns>The reason, or null when it is a JSON object.</returns>
    public static string? TriggerProblem(string trigger)
    {
        try
        {
            return JsonNode.Parse(trigger) is JsonObject ? null : "a run's trigger must be a JSON object such as {\"kind\":\"manual\"}";
        }
        catch (JsonException)   // boundary: text that is not JSON at all is the same refusal
        {
            return "a run's trigger must be a JSON object such as {\"kind\":\"manual\"}, and this is not JSON";
        }
    }
}
