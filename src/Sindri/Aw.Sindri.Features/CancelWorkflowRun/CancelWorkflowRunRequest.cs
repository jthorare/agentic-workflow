using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.CancelWorkflowRun;

/// <summary>Cancel a workflow run (POST /sindri/workflow-runs/{run}/cancel, 'sindri wf cancel').</summary>
/// <param name="Run">The run.</param>
/// <param name="By">Who cancels: 'owner' or 'session:&lt;id&gt;'.</param>
internal sealed record CancelWorkflowRunRequest(WorkflowRunId Run, string By) : ICommand<CancelWorkflowRunResponse>;
