using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The public form of 'workflow.answered' (issue 2426): which run and node got an answer, and the kind of Needs-you row that clears. The answer and who gave it stay in the ledger.</summary>
/// <param name="Run">The run.</param>
/// <param name="Node">The 'ask_owner' node.</param>
/// <param name="Kind">The Needs-you row kind that clears; the ledger row doesn't hold it, so it reads as 'question'.</param>
public sealed record WorkflowAnsweredPublic(WorkflowRunId Run, NodeId Node, string Kind = WorkflowQuestionPublic.QuestionKind);
