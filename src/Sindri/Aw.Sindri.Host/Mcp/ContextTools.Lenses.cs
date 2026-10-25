using System.Text.Json;
using Aw.Context.Core;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

internal static partial class ContextTools
{
    /// <summary>Runs context_lenses. The service is asked for the whole answer and McpPages cuts it, like every other tool.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallLenses(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var bound = BindLenses(arguments);
        if (bound.IsError)
        {
            return McpResults.Failure(services, bound.Error);
        }

        var window = McpArguments.ReadPage(arguments).Value;   // BindLenses read it first, so it is valid here

        var answer = await services.GetRequiredService<IService<GetContextLensesAnswerRequest, LensesAnswer>>().Handle(bound.Value, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return McpResults.Failure(services, answer.Error);
        }

        return answer.Value.Many is { } many
            ? McpResults.Success(services, many, McpSummaries.LensesSet(many), page: window)
            : McpResults.Success(services, answer.Value.One!, McpSummaries.Lenses(answer.Value.One!), page: window);   // why: exactly one of One and Many is set
    }

    /// <summary>The lenses arguments as a request. The page is read first; SND-CONTEXT-022 for anything else. The draft map is never read over MCP.</summary>
    private static Result<GetContextLensesAnswerRequest> BindLenses(IDictionary<string, JsonElement>? arguments)
    {
        var page = McpArguments.ReadPage(arguments);
        if (page.IsError)
        {
            return page.Error;
        }

        var args = McpArguments.ExceptPage(arguments) ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var unknown = args.Keys.FirstOrDefault(k => !_lensesArguments.Contains(k, StringComparer.Ordinal));
        if (unknown is not null)
        {
            return ContextErrors.McpArgumentsInvalid.With($"context_lenses has no argument '{unknown}' (fix: anchor, anchors, mapVersion, hops, limit, offset; the draft map is never read over MCP)");
        }

        var anchors = new List<string>();
        string? mapVersion = null;
        string? hops = null;
        var wrong = args.FirstOrDefault(a => a.Value.ValueKind != JsonValueKind.Null && !Take(a.Key, a.Value, anchors, ref mapVersion, ref hops));
        if (wrong.Key is not null)
        {
            return ContextErrors.McpArgumentsInvalid.With($"'{wrong.Key}' has the wrong type (fix: see the tool's input schema)");
        }

        return anchors.Count == 0 || anchors.Exists(string.IsNullOrWhiteSpace)
            ? ContextErrors.McpArgumentsInvalid.With("context_lenses needs 'anchor', a node key, name or alias (fix: call it with {\"anchor\": \"feature:tabs\"}, or 'anchors' for several)")
            : new GetContextLensesAnswerRequest(anchors, Draft: false, Depth: null, mapVersion, hops, int.MaxValue, 0);   // why: the whole answer; McpPages applies the window
    }

    /// <summary>One lenses argument into the anchors (a single 'anchor' first) or the map version; false for a wrong type.</summary>
    private static bool Take(string name, JsonElement value, List<string> anchors, ref string? mapVersion, ref string? hops)
    {
        if (string.Equals(name, "anchors", StringComparison.Ordinal))
        {
            var items = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : [];
            if (value.ValueKind != JsonValueKind.Array || !items.TrueForAll(e => e.ValueKind == JsonValueKind.String))
            {
                return false;   // checked before any GetString: a number or null item is a wrong type, not a throw
            }

            anchors.AddRange(items.Select(e => e.GetString() ?? string.Empty));
            return true;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        if (string.Equals(name, "anchor", StringComparison.Ordinal))
        {
            anchors.Insert(0, value.GetString() ?? string.Empty);
        }
        else if (string.Equals(name, "hops", StringComparison.Ordinal))
        {
            hops = value.GetString();
        }
        else
        {
            mapVersion = value.GetString();
        }

        return true;
    }
}
