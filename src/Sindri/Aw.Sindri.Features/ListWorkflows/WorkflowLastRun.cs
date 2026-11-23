using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ListWorkflows;

/// <summary>A workflow's newest run in the list row.</summary>
/// <param name="Run">The run.</param>
/// <param name="Status">'queued', 'running', 'waiting', 'succeeded', 'failed' or 'cancelled'.</param>
/// <param name="At">When the run was recorded.</param>
internal sealed record WorkflowLastRun(WorkflowRunId Run, string Status, DateTimeOffset At);
