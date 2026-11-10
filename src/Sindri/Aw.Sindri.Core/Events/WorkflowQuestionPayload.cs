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
public sealed record WorkflowQuestionPayload(WorkflowRunId Run, NodeId Node, string Question, IReadOnlyList<string> Choices, string? Default, DateTimeOffset? TimeoutAt, string By);
