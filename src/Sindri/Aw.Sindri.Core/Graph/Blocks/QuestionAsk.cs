using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What an 'ask_owner' node asks, as <see cref="IQuestionPort"/> takes it.</summary>
/// <param name="Run">The run that asks.</param>
/// <param name="Node">The node that asks.</param>
/// <param name="Question">The question.</param>
/// <param name="Choices">The answers to pick from; empty when any text is an answer.</param>
/// <param name="Default">What the node answers when nobody does by <paramref name="TimeoutAt"/>, or null.</param>
/// <param name="TimeoutAt">When the node stops waiting, or null for never.</param>
public sealed record QuestionAsk(WorkflowRunId Run, NodeId Node, string Question, IReadOnlyList<string> Choices, string? Default, DateTimeOffset? TimeoutAt)
{
    /// <summary>The item's position inside a 'for_each' body (counted from 1) when the question is asked there; null for a node outside a loop.</summary>
    public int? Item { get; init; }

    /// <summary>The id of the workflow that waits for the answer when it is not the run's own (the batch's child workflow of a loop item); null for the run's own.</summary>
    public string? BatchWorkflow { get; init; }
}
