using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>GET /sindri/workflow-runs/{run}: one run with what each of its nodes did.</summary>
/// <param name="Id">The run.</param>
/// <param name="Slug">The workflow.</param>
/// <param name="Revision">The revision it walked.</param>
/// <param name="Status">queued, running, waiting, succeeded, failed or cancelled (read as a string).</param>
/// <param name="Trigger">What started it, as the stored object ("kind", and its payload); kept raw.</param>
/// <param name="Outputs">Its outputs, or null while none.</param>
/// <param name="Error">Why it failed, or null.</param>
/// <param name="ItemId">The item it works for, or null.</param>
/// <param name="StartedAt">When it was recorded.</param>
/// <param name="EndedAt">When it ended, or null while it has not.</param>
/// <param name="Nodes">The node runs, in the order served.</param>
internal sealed record WorkflowRunView(
    string Id,
    string Slug,
    int Revision,
    string Status,
    JsonElement? Trigger,
    JsonElement? Outputs,
    string? Error,
    string? ItemId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    IReadOnlyList<NodeRunView> Nodes);
