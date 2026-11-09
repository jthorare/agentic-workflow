using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.AnswerQuestion;

/// <summary>Answer an 'ask_owner' node's question (POST /sindri/workflow-runs/{run}/nodes/{node}/answer, 'sindri answer'). Exactly one of <paramref name="Text"/> and <paramref name="Choice"/>.</summary>
/// <param name="Run">The run.</param>
/// <param name="Node">The 'ask_owner' node.</param>
/// <param name="Text">A free-text answer, or null.</param>
/// <param name="Choice">One of the node's choices, or null.</param>
/// <param name="By">Who answers: 'owner' or 'session:&lt;id&gt;'.</param>
internal sealed record AnswerQuestionRequest(WorkflowRunId Run, NodeId Node, string? Text, string? Choice, string By) : ICommand<AnswerQuestionResponse>;
