using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The public form of 'workflow.question' (issue 2426): which run and node asked, and the kind of Needs-you row it is. The question, choices and default stay in the ledger.</summary>
/// <param name="Run">The run.</param>
/// <param name="Node">The 'ask_owner' node.</param>
/// <param name="Kind">The Needs-you row kind the question shows as; the ledger row doesn't hold it, so it reads as 'question'.</param>
public sealed record WorkflowQuestionPublic(WorkflowRunId Run, NodeId Node, string Kind = WorkflowQuestionPublic.QuestionKind)
{
    /// <summary>The item inside a 'for_each' body that asks, or null for a node outside a loop.</summary>
    public int? Item { get; init; }

    /// <summary>The Needs-you row kind of an 'ask_owner' question.</summary>
    public const string QuestionKind = "question";
}
