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
        Description = "Answer the question a workflow's ask_owner node is waiting on. Give the run id (<slug>@<revision>:<occurrence>), the node id and exactly one of text or choice; a node inside a for_each loop also needs the item. "
            + "The answer is recorded under your session. The workflow takes it only while that node waits, and a choice must be one the node offers.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["run"] = ContextTools.Property("string", "The run id, e.g. nightly@3:12"),
            ["node"] = ContextTools.Property("string", "The ask_owner node's id, e.g. ask"),
            ["text"] = ContextTools.Property("string", "A free-text answer (when the node offers no choices)"),
            ["choice"] = ContextTools.Property("string", "One of the node's choices"),
            ["item"] = ContextTools.Property("integer", "For an ask_owner node inside a for_each loop: the item's position in the loop, counted from 1", minimum: 1),
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
        var refused = McpArguments.Only(Name, arguments, "run", "node", "text", "choice", "item");
        var run = McpArguments.RequiredString(Name, arguments, "run", "nightly@3:12");
        var node = McpArguments.RequiredString(Name, arguments, "node", "ask");
        var text = McpArguments.OptionalString(Name, arguments, "text");
        var choice = McpArguments.OptionalString(Name, arguments, "choice");
        var item = McpArguments.OptionalLong(Name, arguments, "item", min: 1, max: int.MaxValue);
        if (FirstError(refused, run.E(), node.E(), text.E(), choice.E(), item.E()) is { } error)
        {
            return McpResults.Failure(services, error);
        }

        var ids = Ids(run.Value, node.Value);
        if (ids.IsError)
        {
            return McpResults.Failure(services, ids.Error);
        }

        var by = services.GetRequiredService<ICallerContextAccessor>().Current?.Actor ?? "service:host";
        var answer = await services.GetRequiredService<IService<AnswerQuestionRequest, AnswerQuestionResponse>>()
            .Handle(new AnswerQuestionRequest(ids.Value.Run, ids.Value.Node, text.Value, choice.Value, by, item.Value is { } at ? (int)at : null), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static a => $"answered {a.Run.Value}/{a.Node.Value} as {a.AnsweredBy}");
    }

    private static Error? FirstError(params Error?[] errors) => errors.FirstOrDefault(static e => e is not null);

    private static Result<(WorkflowRunId Run, NodeId Node)> Ids(string run, string node)
    {
        if (!WorkflowRunId.TryFrom(run, out var parsedRun))
        {
            return SindriErrors.InvalidInput.With($"'{run}' is not a run id ('<slug>@<revision>:<occurrence>'; fix: use the run id workflow runs list gives)");
        }

        return NodeId.TryFrom(node, out var parsedNode)
            ? (parsedRun, parsedNode)
            : SindriErrors.InvalidInput.With($"'{node}' is not a node id (a lowercase letter, then lowercase letters, digits and '_')");
    }

    private static Error? E<T>(this Result<T> result) => result.IsError ? result.Error : null;
}
