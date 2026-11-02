using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// context_node and context_neighbors (context spec §8.5, S-3M): each is one call to the ContextCalls service the HTTP route uses,
/// under the 'session:mcp' caller, so the session gate (020) and repo scope (003) apply. No draft argument.
/// </summary>
internal static class GraphTools
{
    /// <summary>context_node's name.</summary>
    internal const string Node = "context_node";

    /// <summary>context_neighbors' name.</summary>
    internal const string Neighbors = "context_neighbors";

    /// <summary>context_node's definition.</summary>
    internal static Tool NodeTool { get; } = new()
    {
        Name = Node,
        Description = "One node of the product graph in this repo by key, name or alias: its label, kind, aliases and the edges going out and coming in. "
            + "Labels are untrusted and fenced." + ContextTools.PageSentence,
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["key"] = ContextTools.Property("string", "A product-graph node key, name or alias"),
            ["limit"] = ContextTools.Property("integer", "How many items to keep from each list (default 100)", minimum: 1),
            ["offset"] = ContextTools.Property("integer", "How many items to skip in each list; omit for the start", minimum: 0),
            ["outside"] = ScopedReads.OutsideProperty(),
            ["reason"] = ScopedReads.ReasonProperty(),
        }, required: ["key"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = false },
    };

    /// <summary>context_neighbors' definition.</summary>
    internal static Tool NeighborsTool { get; } = new()
    {
        Name = Neighbors,
        Description = "The nodes near one product-graph node in this repo, with the edges between them, to a depth (default 1). Labels are untrusted and fenced."
            + ContextTools.PageSentence,
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["key"] = ContextTools.Property("string", "A product-graph node key, name or alias"),
            ["depth"] = ContextTools.Property("integer", "How many edges away to look (default 1; the engine bounds it)", minimum: 0),
            ["limit"] = ContextTools.Property("integer", "How many items to keep from each list (default 100)", minimum: 1),
            ["offset"] = ContextTools.Property("integer", "How many items to skip in each list; omit for the start", minimum: 0),
            ["outside"] = ScopedReads.OutsideProperty(),
            ["reason"] = ScopedReads.ReasonProperty(),
        }, required: ["key"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = false },
    };

    /// <summary>Runs context_node.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallNode(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var page = McpArguments.ReadPage(arguments);
        if (page.IsError)
        {
            return McpResults.Failure(services, page.Error);
        }

        var split = ScopedReads.Separate(Node, arguments);
        if (split.IsError)
        {
            return McpResults.Failure(services, split.Error);
        }

        var bound = BindKey(Node, split.Value.Rest, withDepth: false);
        if (bound.IsError)
        {
            return McpResults.Failure(services, bound.Error);
        }

        var answer = await services.GetRequiredService<IService<ShowContextNodeRequest, NodeView>>().Handle(new ShowContextNodeRequest(bound.Value.Key), ct).ConfigureAwait(false);

        // why: the key may be a name or an alias, so the graph check uses the node key the read resolved it to
        return answer.IsSuccess && await ScopedReads.Check(services, Node, answer.Value.Node.Key.Value, split.Value, ct).ConfigureAwait(false) is { } refused
            ? McpResults.Failure(services, refused)
            : McpResults.From(services, answer, McpSummaries.Node, page: page.Value);
    }

    /// <summary>Runs context_neighbors.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallNeighbors(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var page = McpArguments.ReadPage(arguments);
        if (page.IsError)
        {
            return McpResults.Failure(services, page.Error);
        }

        var split = ScopedReads.Separate(Neighbors, arguments);
        if (split.IsError)
        {
            return McpResults.Failure(services, split.Error);
        }

        var bound = BindKey(Neighbors, split.Value.Rest, withDepth: true);
        if (bound.IsError)
        {
            return McpResults.Failure(services, bound.Error);
        }

        var answer = await services.GetRequiredService<IService<ListContextNeighborsRequest, NeighborsView>>()
            .Handle(new ListContextNeighborsRequest(bound.Value.Key, bound.Value.Depth), ct).ConfigureAwait(false);

        // why: the key may be a name or an alias, so the graph check uses the node key the read resolved it to
        return answer.IsSuccess && await ScopedReads.Check(services, Neighbors, answer.Value.Center.Value, split.Value, ct).ConfigureAwait(false) is { } refused
            ? McpResults.Failure(services, refused)
            : McpResults.From(services, answer, McpSummaries.Neighbors, page: page.Value);
    }

    /// <summary>The key (and depth) of a graph call; SND-CONTEXT-022 for a missing or blank key, an unknown property or a wrong type.</summary>
    private static Result<(string Key, string? Depth)> BindKey(string tool, IDictionary<string, JsonElement>? arguments, bool withDepth)
    {
        var unknown = (arguments?.Keys ?? []).FirstOrDefault(k => !IsKnown(k, withDepth));
        if (unknown is not null)
        {
            return ContextErrors.McpArgumentsInvalid.With($"{tool} has no argument '{unknown}' (fix: {(withDepth ? "key, depth, limit, offset" : "key, limit, offset")} only; the draft map is never read over MCP)");
        }

        if (arguments is null || !arguments.TryGetValue("key", out var key) || key.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(key.GetString()))
        {
            return ContextErrors.McpArgumentsInvalid.With($"{tool} needs 'key', a node key, name or alias (fix: call it with {{\"key\": \"feature:tabs\"}})");
        }

        var depth = BindDepth(arguments);
        if (depth.IsError)
        {
            return depth.Error;
        }

        // why: the check above found a non-blank string, so GetString is not null
        return (key.GetString()!, depth.Value);
    }

    private static bool IsKnown(string name, bool withDepth) =>
        string.Equals(name, "key", StringComparison.Ordinal)
        || McpArguments.PageNames.Contains(name, StringComparer.Ordinal)
        || (withDepth && string.Equals(name, "depth", StringComparison.Ordinal));

    /// <summary>The optional depth as the text the service takes; SND-CONTEXT-022 unless it is an integer. A null is absent.</summary>
    private static Result<string?> BindDepth(IDictionary<string, JsonElement> arguments)
    {
        if (!arguments.TryGetValue("depth", out var raw) || raw.ValueKind == JsonValueKind.Null)
        {
            return (string?)null;
        }

        return raw.ValueKind == JsonValueKind.Number && raw.TryGetInt32(out var parsed)
            ? parsed.ToString(CultureInfo.InvariantCulture)
            : ContextErrors.McpArgumentsInvalid.With($"'depth' must be an integer, got {raw.ValueKind.ToString().ToLowerInvariant()} (fix: see the tool's input schema)");
    }
}
