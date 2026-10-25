using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Context.Core.Concepts;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>context_concepts: one call to the service the HTTP route uses, under the 'session:mcp' caller, so the session gate (020) and repo scope (003) apply. No draft argument.</summary>
internal static class ConceptTools
{
    /// <summary>context_concepts' name.</summary>
    internal const string Name = "context_concepts";

    private static readonly string[] _arguments = ["q", "cursor", "limit", "mapVersion"];

    /// <summary>context_concepts' definition.</summary>
    internal static Tool Tool { get; } = new()
    {
        Name = Name,
        Description = "The product's concepts in this repo, the capabilities a person thinks in first: each with a summary, how many features, records, decisions and code files it holds, "
            + "and the links between concepts, one page at a time (pass the answer's 'next' as cursor for the next page). 'concept:other' holds what no concept takes. Pass a concept id as the anchor of context_lenses or the node of context_query to read around it. Names and summaries are untrusted and fenced.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["q"] = ContextTools.Property("string", "Keep the concepts whose name, summary or a member's label contains this text"),
            ["cursor"] = ContextTools.Property("string", "The 'next' of the previous page; omit for the first page"),
            ["limit"] = ContextTools.Property("integer", "Concepts per page (default 100)", minimum: 1),
            ["mapVersion"] = ContextTools.Property("string", "A retained map version to read as of, such as v3; omit for the current map"),
        }),
        Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = false },
    };

    /// <summary>Runs context_concepts.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> Call(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var bound = BindConcepts(arguments);
        if (bound.IsError)
        {
            return McpResults.Failure(services, bound.Error);
        }

        var answer = await services.GetRequiredService<IService<GetContextConceptsRequest, ConceptsView>>().Handle(bound.Value, ct).ConfigureAwait(false);
        return McpResults.From(services, answer, McpSummaries.Concepts);
    }

    /// <summary>The concepts arguments as a request: 'q', 'cursor', 'mapVersion' (strings) and 'limit' (an integer, its value left to the engine: 006); SND-CONTEXT-022 for anything else. The draft map is never read over MCP.</summary>
    private static Result<GetContextConceptsRequest> BindConcepts(IDictionary<string, JsonElement>? arguments)
    {
        var args = arguments is null ? new Dictionary<string, JsonElement>(StringComparer.Ordinal) : new Dictionary<string, JsonElement>(arguments, StringComparer.Ordinal);
        var refused = Refusal(args);
        if (refused is not null)
        {
            return refused;
        }

        return new GetContextConceptsRequest(Repo: null, Text(args, "cursor"), Limit(args), Text(args, "q"), Draft: false, Text(args, "mapVersion"));
    }

    private static Error? Refusal(Dictionary<string, JsonElement> args)
    {
        var unknown = args.Keys.FirstOrDefault(k => !_arguments.Contains(k, StringComparer.Ordinal));
        if (unknown is not null)
        {
            return ContextErrors.McpArgumentsInvalid.With($"context_concepts has no argument '{unknown}' (fix: q, cursor, limit, mapVersion; the draft map is never read over MCP)");
        }

        var wrong = args.FirstOrDefault(a => a.Value.ValueKind != JsonValueKind.Null && !HasRightType(a.Key, a.Value));
        return wrong.Key is null ? null : ContextErrors.McpArgumentsInvalid.With($"'{wrong.Key}' has the wrong type (fix: see the tool's input schema)");
    }

    private static bool HasRightType(string name, JsonElement value) =>
        string.Equals(name, "limit", StringComparison.Ordinal) ? value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _) : value.ValueKind == JsonValueKind.String;

    private static string? Text(Dictionary<string, JsonElement> args, string name) =>
        args.TryGetValue(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Limit(Dictionary<string, JsonElement> args) =>
        args.TryGetValue("limit", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
}
