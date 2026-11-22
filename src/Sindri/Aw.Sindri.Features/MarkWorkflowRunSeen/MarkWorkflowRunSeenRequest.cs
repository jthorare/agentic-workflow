using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.MarkWorkflowRunSeen;

/// <summary>The owner opened a failed run (POST /sindri/workflow-runs/{run}/seen): its Home row clears.</summary>
/// <param name="Run">The run.</param>
internal sealed record MarkWorkflowRunSeenRequest(WorkflowRunId Run) : ICommand<MarkWorkflowRunSeenResponse>;
