using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Context.Core.Pages;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>context_pages: one call to the service the HTTP route uses, under the 'session:mcp' caller, so the session gate (020) and repo scope (003) apply. Owner and suggested pages both come back, marked; no draft argument.</summary>
internal static class PageTools
{
    /// <summary>context_pages' name.</summary>
    internal const string Name = "context_pages";

    private static readonly string[] _numbers = ["limit", "offset"];
    private static readonly string[] _arguments = ["concept", "page", "repo", "limit", "offset", "mapVersion"];

    /// <summary>context_pages' definition.</summary>
    internal static Tool Tool { get; } = new()
    {
        Name = Name,
        Description = "The product's pages in this repo, each with its actions and the concepts they express: the pages a person named first (mark 'owner'), then the ones the map suggests (mark 'suggested', most actions first), "
            + "and whether a code file implements each. Pass a concept id to keep the pages that express it, or a page id for one page. Page and action names are untrusted and fenced.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["concept"] = ContextTools.Property("string", "Keep the pages that express this concept (an id or concept:id) or have an action that does"),
            ["page"] = ContextTools.Property("string", "Keep the one page with this id"),
            ["repo"] = ContextTools.Property("string", "Keep the pages of this repo"),
            ["limit"] = ContextTools.Property("integer", "Pages per answer (default 50, at most 200)", minimum: 1),
            ["offset"] = ContextTools.Property("integer", "Pages to skip (default 0)", minimum: 0),
            ["mapVersion"] = ContextTools.Property("string", "A retained map version to read as of, such as v3; omit for the current map"),
        }),
        Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = false },
    };

    /// <summary>Runs context_pages.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> Call(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var bound = Bind(arguments);
        if (bound.IsError)
        {
            return McpResults.Failure(services, bound.Error);
        }

        var answer = await services.GetRequiredService<IService<GetContextPagesRequest, PagesView>>().Handle(bound.Value, ct).ConfigureAwait(false);
        return McpResults.From(services, answer, McpSummaries.Pages);
    }

    /// <summary>The arguments as a request: 'concept', 'page', 'repo', 'mapVersion' (strings) and 'limit', 'offset' (integers, their range left to the engine); SND-CONTEXT-022 for anything else. The draft map is never read over MCP.</summary>
    private static Result<GetContextPagesRequest> Bind(IDictionary<string, JsonElement>? arguments)
    {
        var args = arguments is null ? new Dictionary<string, JsonElement>(StringComparer.Ordinal) : new Dictionary<string, JsonElement>(arguments, StringComparer.Ordinal);
        var unknown = args.Keys.FirstOrDefault(k => !_arguments.Contains(k, StringComparer.Ordinal));
        if (unknown is not null)
        {
            return ContextErrors.McpArgumentsInvalid.With($"context_pages has no argument '{unknown}' (fix: concept, page, repo, limit, offset, mapVersion; the draft map is never read over MCP)");
        }

        var wrong = args.FirstOrDefault(a => a.Value.ValueKind != JsonValueKind.Null && !HasRightType(a.Key, a.Value));
        return wrong.Key is not null
            ? ContextErrors.McpArgumentsInvalid.With($"'{wrong.Key}' has the wrong type (fix: see the tool's input schema)")
            : new GetContextPagesRequest(Text(args, "repo"), Text(args, "concept"), Text(args, "page"), PageMarks.All, Number(args, "limit"), Number(args, "offset"), Draft: false, Text(args, "mapVersion"));
    }

    private static bool HasRightType(string name, JsonElement value) =>
        _numbers.Contains(name, StringComparer.Ordinal) ? value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _) : value.ValueKind == JsonValueKind.String;

    private static string? Text(Dictionary<string, JsonElement> args, string name) =>
        args.TryGetValue(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Number(Dictionary<string, JsonElement> args, string name) =>
        args.TryGetValue(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
}
