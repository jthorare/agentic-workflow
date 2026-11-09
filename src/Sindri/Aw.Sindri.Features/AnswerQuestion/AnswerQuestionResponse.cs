using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.AnswerQuestion;

/// <summary>The workflow took the answer.</summary>
/// <param name="Run">The run.</param>
/// <param name="Node">The 'ask_owner' node.</param>
/// <param name="Answer">The answer as the node hands it on.</param>
/// <param name="AnsweredBy">Who answered, as the timeline shows it.</param>
internal sealed record AnswerQuestionResponse(WorkflowRunId Run, NodeId Node, string Answer, string AnsweredBy);
