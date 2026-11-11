using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.GetWorkflowRun;

/// <summary>Read one run with its node runs (GET /sindri/workflow-runs/{run}, 'sindri wf run-show', MCP workflow_run_get).</summary>
/// <param name="Run">The run.</param>
internal sealed record GetWorkflowRunRequest(WorkflowRunId Run) : IQuery<GetWorkflowRunResponse>;
