using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.AnswerQuestion;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// workflow_answer (agent-first PR 9): answers an 'ask_owner' node's question, one call to the AnswerQuestion service 'POST /sindri/workflow-runs/{run}/nodes/{node}/answer' calls.
/// Any caller may answer; the answer is attributed to the caller, so a launched session that answers its own run's question shows as 'session:launch:&lt;launch id&gt;' on the timeline.
/// </summary>
internal static class WorkflowAnswerTool
{
    /// <summary>workflow_answer's name.</summary>
    internal const string Name = "workflow_answer";

    /// <summary>The tool's definition.</summary>
    internal static Tool Tool { get; } = new()
    {
        Name = Name,
        Description = "Answer the question a workflow's ask_owner node is waiting on. Give the run id (<slug>@<revision>:<occurrence>), the node id and exactly one of text or choice. "
            + "The answer is recorded under your session. The workflow takes it only while that node waits, and a choice must be one the node offers.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["run"] = ContextTools.Property("string", "The run id, e.g. nightly@3:12"),
            ["node"] = ContextTools.Property("string", "The ask_owner node's id, e.g. ask"),
            ["text"] = ContextTools.Property("string", "A free-text answer (when the node offers no choices)"),
            ["choice"] = ContextTools.Property("string", "One of the node's choices"),
        }, required: ["run", "node"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = false, IdempotentHint = false, OpenWorldHint = false },
    };

    /// <summary>Runs workflow_answer.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> Call(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var refused = McpArguments.Only(Name, arguments, "run", "node", "text", "choice");
        var run = McpArguments.RequiredString(Name, arguments, "run", "nightly@3:12");
        var node = McpArguments.RequiredString(Name, arguments, "node", "ask");
        var text = McpArguments.OptionalString(Name, arguments, "text");
        var choice = McpArguments.OptionalString(Name, arguments, "choice");
        if ((refused ?? run.E() ?? node.E() ?? text.E() ?? choice.E()) is { } error)
        {
            return McpResults.Failure(services, error);
        }

        if (!WorkflowRunId.TryFrom(run.Value, out var parsedRun))
        {
            return McpResults.Failure(services, SindriErrors.InvalidInput.With($"'{run.Value}' is not a run id ('<slug>@<revision>:<occurrence>'; fix: use the run id workflow runs list gives)"));
        }

        if (!NodeId.TryFrom(node.Value, out var parsedNode))
        {
            return McpResults.Failure(services, SindriErrors.InvalidInput.With($"'{node.Value}' is not a node id (a lowercase letter, then lowercase letters, digits and '_')"));
        }

        var by = services.GetRequiredService<ICallerContextAccessor>().Current?.Actor ?? "service:host";
        var answer = await services.GetRequiredService<IService<AnswerQuestionRequest, AnswerQuestionResponse>>()
            .Handle(new AnswerQuestionRequest(parsedRun, parsedNode, text.Value, choice.Value, by), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static a => $"answered {a.Run.Value}/{a.Node.Value} as {a.AnsweredBy}");
    }

    private static Error? E<T>(this Result<T> result) => result.IsError ? result.Error : null;
}
