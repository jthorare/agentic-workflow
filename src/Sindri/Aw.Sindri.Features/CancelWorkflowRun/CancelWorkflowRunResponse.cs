using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.CancelWorkflowRun;

/// <summary>The cancel was delivered to the run; it ends as 'cancelled' when its workflow stops.</summary>
/// <param name="Run">The run.</param>
internal sealed record CancelWorkflowRunResponse(WorkflowRunId Run);
