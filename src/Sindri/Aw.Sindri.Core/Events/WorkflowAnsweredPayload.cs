using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'workflow.answered' payload (issue 2258, PR 9): someone answered an 'ask_owner' node; the timeline names who.</summary>
/// <param name="Run">The run.</param>
/// <param name="Node">The 'ask_owner' node.</param>
/// <param name="Answer">The answer.</param>
/// <param name="By">Who answered: 'owner', 'session:&lt;launch id&gt;' (a launched session; another session shows its client, 'session:mcp') (an agent answering its own run's question is shown as that session).</param>
public sealed record WorkflowAnsweredPayload(WorkflowRunId Run, NodeId Node, string Answer, string By);
