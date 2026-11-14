using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.RerunWorkflowRun;

/// <summary>Re-run an ended run from one of its steps (POST /sindri/workflow-runs/{run}/rerun, 'sindri wf rerun', MCP workflow_rerun; Workflows page spec §7.3, P7).</summary>
/// <param name="Run">The ended run to start from.</param>
/// <param name="FromNode">The step to re-run first; it and every step after it run again, the steps before it keep their stored output.</param>
/// <param name="Input">A JSON object that replaces the chosen step's stored input for the new run, or null to reuse it.</param>
/// <param name="By">Who asked: 'owner' or 'session:&lt;id&gt;'.</param>
internal sealed record RerunWorkflowRunRequest(WorkflowRunId Run, string FromNode, string? Input, string By) : ICommand<RerunWorkflowRunResponse>;
