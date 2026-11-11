using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.AnswerQuestion;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/workflow-runs/{run}/nodes/{node}/answer ('sindri answer', MCP workflow_answer): answers an 'ask_owner' node's question; any caller may, and the answer is attributed to the caller.</summary>
internal static class AnswerQuestionEndpoint
{
    internal static RouteGroupBuilder MapAnswerQuestion(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow-runs/{run}/nodes/{node}/answer", static async (string run, string node, AnswerQuestionBody? body, IService<AnswerQuestionRequest, AnswerQuestionResponse> service, ICallerContextAccessor callers, CancellationToken ct) =>
        {
            if (!WorkflowRunId.TryFrom(run, out var parsedRun))
            {
                return ((Result<AnswerQuestionResponse>)SindriErrors.InvalidInput.With($"'{run}' is not a run id ('<slug>@<revision>:<occurrence>')")).ToHttpResult();
            }

            if (!NodeId.TryFrom(node, out var parsedNode))
            {
                return ((Result<AnswerQuestionResponse>)SindriErrors.InvalidInput.With($"'{node}' is not a node id (a lowercase letter, then lowercase letters, digits and '_')")).ToHttpResult();
            }

            var by = callers.Current?.Actor ?? "service:host";
            return (await service.Handle(new AnswerQuestionRequest(parsedRun, parsedNode, body?.Text, body?.Choice, by, body?.Item), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
