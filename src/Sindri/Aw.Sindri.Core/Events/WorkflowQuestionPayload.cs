using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'workflow.question' payload (issue 2258, PR 9): an 'ask_owner' node asked; the question waits as a needs-you row until it is answered or its run ends.</summary>
/// <param name="Run">The run.</param>
/// <param name="Node">The 'ask_owner' node.</param>
/// <param name="Question">The question.</param>
/// <param name="Choices">The answers to pick from; empty when any text is an answer.</param>
/// <param name="Default">What the node answers at its timeout, or null.</param>
/// <param name="TimeoutAt">When the node stops waiting, or null for never.</param>
/// <param name="By">Who posted it, as the envelope's actor reads.</param>
public sealed record WorkflowQuestionPayload(WorkflowRunId Run, NodeId Node, string Question, IReadOnlyList<string> Choices, string? Default, DateTimeOffset? TimeoutAt, string By)
{
    /// <summary>The item's position inside a 'for_each' body (counted from 1) when the question is asked there; null otherwise.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? Item { get; init; }

    /// <summary>The id of the workflow that waits for the answer when it is not the run's own (a loop batch's child); null otherwise.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? BatchWorkflow { get; init; }
}
