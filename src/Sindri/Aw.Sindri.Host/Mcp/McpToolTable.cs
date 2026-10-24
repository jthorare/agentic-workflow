using System.Collections.Frozen;
using System.Diagnostics;
using Aw.Context.Core;
using Aw.Sindri.Core.Commands;
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
    private static readonly IReadOnlyList<Tool> _tools = [ContextTools.QueryTool, ContextTools.SourcesTool, ContextTools.LensesTool, GraphTools.NodeTool, GraphTools.NeighborsTool,
        SindriTools.StatusTool, SindriTools.QueueTool, SindriTools.ItemTool, SindriTools.TimelineTool, SindriTools.PendingDecisionsTool, SindriTools.JobTool, SindriTools.RequestTool];

    // why: only the context and graph tools read one repo's map; the sindri_* tools answer for every repo, as the same call does for a human
    private static readonly FrozenSet<string> _repoScoped = FrozenSet.Create(StringComparer.Ordinal,
        ContextTools.Query, ContextTools.Sources, ContextTools.Lenses, GraphTools.Node, GraphTools.Neighbors);

    /// <summary>tools/list: every tool, whatever the profile says (a refused call explains itself; context spec §8.6).</summary>
    /// <param name="_">The request (unused).</param>
    /// <param name="__">Unused.</param>
    /// <returns>The tool list.</returns>
    internal static ValueTask<ListToolsResult> List(RequestContext<ListToolsRequestParams> _, CancellationToken __)
        => ValueTask.FromResult(new ListToolsResult { Tools = [.. _tools] });

    /// <summary>tools/call: the named tool, or SND-CONTEXT-023.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static ValueTask<CallToolResult> Call(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
    {
        var call = request.Params!;           // why: the SDK sets Params on every tools/call
        var services = request.Services!;     // why: the SDK sets the request's services on every request
        if (_tools.All(t => t.Name != call.Name))
        {
            return ValueTask.FromResult(McpResults.Failure(services, ContextErrors.McpToolUnknown.With(
                $"'{call.Name}' is not a Sindri MCP tool (fix: tools/list names them)")));
        }

        if (_repoScoped.Contains(call.Name)
            && services.GetRequiredService<ICallerContextAccessor>().Current?.Repo is null)
        {
            return ValueTask.FromResult(McpResults.Failure(services, ContextErrors.McpRepoMissing.With(
                "the call has no X-Sindri-Repo header, so it has no repo scope (fix: run 'sindri mcp register' in the work repo and use that registration)")));
        }

        return call.Name switch
        {
            ContextTools.Query => ContextTools.CallQuery(services, call.Arguments, cancellationToken),
            ContextTools.Sources => ContextTools.CallSources(services, call.Arguments, cancellationToken),
            ContextTools.Lenses => ContextTools.CallLenses(services, call.Arguments, cancellationToken),
            GraphTools.Node => GraphTools.CallNode(services, call.Arguments, cancellationToken),
            GraphTools.Neighbors => GraphTools.CallNeighbors(services, call.Arguments, cancellationToken),
            SindriTools.Status => SindriTools.CallStatus(services, call.Arguments, cancellationToken),
            SindriTools.Queue => SindriTools.CallQueue(services, call.Arguments, cancellationToken),
            SindriTools.Item => SindriTools.CallItem(services, call.Arguments, cancellationToken),
            SindriTools.Timeline => SindriTools.CallTimeline(services, call.Arguments, cancellationToken),
            SindriTools.PendingDecisions => SindriTools.CallPendingDecisions(services, call.Arguments, cancellationToken),
            SindriTools.Job => SindriTools.CallJob(services, call.Arguments, cancellationToken),
            SindriTools.Request => SindriTools.CallRequest(services, call.Arguments, cancellationToken),
            _ => throw new UnreachableException($"McpToolTable lists '{call.Name}' but has no handler for it"),
        };
    }
}
