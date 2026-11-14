using System.Collections.Frozen;
using System.Diagnostics;
using Aw.Context.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Toolkits;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// The MCP tools, registered explicitly (one row per tool; API P8 appends its sindri_* rows). Low-level handlers rather
/// than the SDK's attribute style, so each input schema is written by hand and every refusal is a tool error carrying its
/// SND code (docs/readiness/aot-spike.md: the attribute style answers bad arguments with a generic message and an unknown
/// tool with a JSON-RPC error).
/// </summary>
internal static class McpToolTable
{
    private static readonly IReadOnlyList<Tool> _tools = [ContextTools.QueryTool, ContextTools.SourcesTool, ContextTools.LensesTool, ConceptTools.Tool, PageTools.Tool, GraphTools.NodeTool, GraphTools.NeighborsTool,
        SindriTools.StatusTool, SindriTools.QueueTool, SindriTools.ItemTool, SindriTools.TimelineTool, SindriTools.PendingDecisionsTool, SindriTools.JobTool, SindriTools.ProjectsTool, SindriTools.RequestTool, SindriTools.HandoffTool, SindriTools.TodosTool, SindriTools.TodoTool, .. ConnectionTools.All, .. MemoryTools.All, .. WorkflowReadTools.All, .. WorkflowTools.All, WorkflowAnswerTool.Tool, .. ResourceTools.All, .. AgentTools.All, .. ToolkitTools.Management];

    // why: only the context and graph tools read one repo's map; the sindri_* tools answer for every repo, as the same call does for a human
    private static readonly FrozenSet<string> _repoScoped = FrozenSet.Create(StringComparer.Ordinal,
        ContextTools.Query, ContextTools.Sources, ContextTools.Lenses, ConceptTools.Name, PageTools.Name, GraphTools.Node, GraphTools.Neighbors);

    /// <summary>tools/list: every tool, whatever the profile says (a refused call explains itself; context spec §8.6).</summary>
    /// <param name="request">The request.</param>
    /// <param name="_">Unused.</param>
    /// <returns>The static tools, then the tools of every running, unpaused toolkit.</returns>
    internal static ValueTask<ListToolsResult> List(RequestContext<ListToolsRequestParams> request, CancellationToken _)
        => ValueTask.FromResult(new ListToolsResult { Tools = [.. _tools, .. ToolkitTools.Running(request.Services?.GetService<IRunningToolkits>())] });

    /// <summary>tools/call: the named tool, or SND-CONTEXT-023.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static ValueTask<CallToolResult> Call(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
    {
        var call = request.Params!;           // why: the SDK sets Params on every tools/call
        var services = request.Services!;     // why: the SDK sets the request's services on every request
        if (_tools.All(t => t.Name != call.Name) && ToolkitTools.IsToolkitTool(call.Name))
        {
            return ToolkitTools.Call(services, call.Name, call.Arguments, cancellationToken);
        }

        if (_tools.All(t => t.Name != call.Name))
        {
            return ValueTask.FromResult(McpResults.Failure(services, ContextErrors.McpToolUnknown.With(
                $"'{call.Name}' is not a Sindri MCP tool (fix: tools/list names them)")));
        }

        if (RepoMissing(call.Name, services) is { } refused)
        {
            return ValueTask.FromResult(refused);
        }

        return Dispatch(services, call, cancellationToken);
    }

    /// <summary>Calls the handler of a known tool.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="call">The tools/call parameters.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    private static ValueTask<CallToolResult> Dispatch(IServiceProvider services, CallToolRequestParams call, CancellationToken cancellationToken)
    {
        return call.Name switch
        {
            ContextTools.Query => ContextTools.CallQuery(services, call.Arguments, cancellationToken),
            ContextTools.Sources => ContextTools.CallSources(services, call.Arguments, cancellationToken),
            ContextTools.Lenses => ContextTools.CallLenses(services, call.Arguments, cancellationToken),
            ConceptTools.Name => ConceptTools.Call(services, call.Arguments, cancellationToken),
            PageTools.Name => PageTools.Call(services, call.Arguments, cancellationToken),
            GraphTools.Node => GraphTools.CallNode(services, call.Arguments, cancellationToken),
            GraphTools.Neighbors => GraphTools.CallNeighbors(services, call.Arguments, cancellationToken),
            SindriTools.Status => SindriTools.CallStatus(services, call.Arguments, cancellationToken),
            SindriTools.Queue => SindriTools.CallQueue(services, call.Arguments, cancellationToken),
            SindriTools.Item => SindriTools.CallItem(services, call.Arguments, cancellationToken),
            SindriTools.Timeline => SindriTools.CallTimeline(services, call.Arguments, cancellationToken),
            SindriTools.PendingDecisions => SindriTools.CallPendingDecisions(services, call.Arguments, cancellationToken),
            SindriTools.Job => SindriTools.CallJob(services, call.Arguments, cancellationToken),
            SindriTools.Projects => SindriTools.CallProjects(services, call.Arguments, cancellationToken),
            SindriTools.Request => SindriTools.CallRequest(services, call.Arguments, cancellationToken),
            SindriTools.Handoff => SindriTools.CallHandoff(services, call.Arguments, cancellationToken),
            SindriTools.Todos => SindriTools.CallTodos(services, call.Arguments, cancellationToken),
            SindriTools.Todo => SindriTools.CallTodo(services, call.Arguments, cancellationToken),
            ConnectionTools.List or ConnectionTools.Get or ConnectionTools.Add or ConnectionTools.SignIn or ConnectionTools.Test or ConnectionTools.Remove => ConnectionTools.Call(call.Name, services, call.Arguments, cancellationToken),
            WorkflowAnswerTool.Name => WorkflowAnswerTool.Call(services, call.Arguments, cancellationToken),
            WorkflowTools.Create or WorkflowTools.ApplyOps or WorkflowTools.Replace or WorkflowTools.Validate or WorkflowTools.Activate or WorkflowTools.Pause or WorkflowTools.Delete or WorkflowTools.Run or WorkflowTools.Cancel or WorkflowTools.Docs or WorkflowTools.Blocks
                => WorkflowTools.Call(call.Name, services, call.Arguments, cancellationToken),
            WorkflowReadTools.List or WorkflowReadTools.Get or WorkflowReadTools.Runs or WorkflowReadTools.RunGet => WorkflowReadTools.Call(call.Name, services, call.Arguments, cancellationToken),
            ToolkitTools.List or ToolkitTools.Create or ToolkitTools.Build or ToolkitTools.Start or ToolkitTools.Stop or ToolkitTools.Pause or ToolkitTools.Resume or ToolkitTools.Calls => ToolkitTools.CallManagement(call.Name, services, call.Arguments, cancellationToken),
            ResourceTools.List or ResourceTools.Purge => ResourceTools.Call(call.Name, services, call.Arguments, cancellationToken),
            AgentTools.Start or AgentTools.RemoveWorktree or AgentTools.Stop => AgentTools.Call(call.Name, services, call.Arguments, cancellationToken),
            MemoryTools.List => MemoryTools.CallList(services, call.Arguments, cancellationToken),
            MemoryTools.Create => MemoryTools.CallCreate(services, call.Arguments, cancellationToken),
            MemoryTools.Search => MemoryTools.CallSearch(services, call.Arguments, cancellationToken),
            MemoryTools.Write => MemoryTools.CallWrite(services, call.Arguments, cancellationToken),
            MemoryTools.Forget => MemoryTools.CallForget(services, call.Arguments, cancellationToken),
            _ => throw new UnreachableException($"McpToolTable lists '{call.Name}' but has no handler for it"),
        };
    }

    private static CallToolResult? RepoMissing(string tool, IServiceProvider services) =>
        _repoScoped.Contains(tool) && services.GetRequiredService<ICallerContextAccessor>().Current?.Repo is null
            ? McpResults.Failure(services, ContextErrors.McpRepoMissing.With(
                "the call has no X-Sindri-Repo header, so it has no repo scope (fix: run 'sindri mcp register' in the work repo and use that registration)"))
            : null;
}
