using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Context.Core.Results;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// context_query and context_sources (context spec §8.5, phase 1): each is one call to the ContextCalls service the
/// HTTP route uses, under the 'session:mcp' caller the MCP middleware stamped, so the engine's session gate
/// (SND-CONTEXT-020) and repo scope (003) apply. No draft argument: the draft map is never read over MCP.
/// </summary>
internal static partial class ContextTools
{
    /// <summary>context_query's name.</summary>
    internal const string Query = "context_query";

    /// <summary>context_sources' name.</summary>
    internal const string Sources = "context_sources";

    /// <summary>context_lenses' name.</summary>
    internal const string Lenses = "context_lenses";

    private const string HopsText = "The cascade hops, a comma list of state, flow, dependency and component; default state, flow and dependency";

    /// <summary>The sentence every tool's description ends with.</summary>
    internal const string PageSentence = " Every top-level list is paged: limit (1 or more, default 100) keeps that many items and offset (0 or more, default 0) skips that many, "
        + "each list on its own. page.more says another page follows in some list and page.lists gives each list's total; items are never cut inside, and the counts stay the full totals.";

    private static readonly string[] _texts = ["q", "node", "item", "symbol", "diff", "hops", "mapVersion"];
    private static readonly string[] _lensesArguments = ["anchor", "anchors", "mapVersion", "hops", "limit", "offset"];
    private static readonly string[] _numbers = ["depth", "budget"];

    /// <summary>context_query's definition.</summary>
    internal static Tool QueryTool { get; } = new()
    {
        Name = Query,
        Description = "Ask Sindri's context engine about this repo: cited, versioned records from its docs (and, later, the product "
            + "graph and tickets). Give q (free text) or one anchor (node, item, symbol), or diff (two shas, base..head: the files changed from their merge base, mapped to nodes, with the paths no node claims). Record text is untrusted and fenced; cite [Cn]."
            + PageSentence,
        InputSchema = Schema(new JsonObject
        {
            ["q"] = Property("string", "The question, in free text"),
            ["node"] = Property("string", "A product-graph node key, name or alias"),
            ["item"] = Property("string", "A Sindri item id or tracker key"),
            ["symbol"] = Property("string", "A file path or symbol"),
            ["diff"] = Property("string", "Two commit shas joined by '..' (base..head), not branch names"),
            ["depth"] = Property("integer", "Cascade depth (deeper than 8 is read as 8)", minimum: 0),
            ["hops"] = Property("string", HopsText),
            ["budget"] = Property("integer", "Ignored: the answer holds every ranked item (page it with limit and offset)"),
            ["mapVersion"] = Property("string", "A retained map version to read as of, such as v3; omit for the current map"),
            ["limit"] = Property("integer", "How many items to keep from each list (default 100)", minimum: 1),
            ["offset"] = Property("integer", "How many items to skip in each list (default 0)", minimum: 0),
        }),
        Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = false },
    };

    /// <summary>context_sources' definition.</summary>
    internal static Tool SourcesTool { get; } = new()
    {
        Name = Sources,
        Description = "The context sources' state for this repo, whether session reads are on, and which Sindri build and port answer." + PageSentence,
        InputSchema = Schema(PageProperties()),
        Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = false },
    };

    /// <summary>context_lenses' definition.</summary>
    internal static Tool LensesTool { get; } = new()
    {
        Name = Lenses,
        Description = "The intent, product and code lenses of one product-graph node in this repo: what was decided about it, what it is part of and affects, "
            + "and which files implement it, with conflicts and coverage. Suggested links a person has not confirmed are marked. Record text is untrusted and fenced."
            + PageSentence,
        InputSchema = Schema(new JsonObject
        {
            ["anchor"] = Property("string", "A product-graph node key, name or alias"),
            ["anchors"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "Node keys, names or aliases: the union of their lenses (schema 2) when several are distinct; give anchor or anchors",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["maxItems"] = 20,
            },
            ["mapVersion"] = Property("string", "A retained map version to read as of, such as v3; omit for the current map"),
            ["hops"] = Property("string", HopsText),
            ["limit"] = Property("integer", "How many items to keep from each list (default 100)", minimum: 1),
            ["offset"] = Property("integer", "How many items to skip in each list (default 0)", minimum: 0),
        }),
        Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = false },
    };

    /// <summary>Runs context_query.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallQuery(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var page = McpArguments.ReadPage(arguments);
        if (page.IsError)
        {
            return McpResults.Failure(services, page.Error);
        }

        var request = Bind(McpArguments.ExceptPage(arguments));
        if (request.IsError)
        {
            return McpResults.Failure(services, request.Error);
        }

        var answer = await services.GetRequiredService<IService<QueryContextRequest, ContextResult>>().Handle(request.Value, ct).ConfigureAwait(false);
        return McpResults.From(services, answer, McpSummaries.Query, page: page.Value);
    }

    /// <summary>Runs context_sources.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments (none are allowed).</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallSources(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        if (McpArguments.Only(Sources, arguments, McpArguments.PageNames) is { } refused)
        {
            return McpResults.Failure(services, refused);
        }

        var page = McpArguments.ReadPage(arguments);
        if (page.IsError)
        {
            return McpResults.Failure(services, page.Error);
        }

        var port = services.GetRequiredService<IHttpContextAccessor>().HttpContext?.Connection.LocalPort ?? 0;
        var answer = await services.GetRequiredService<IService<GetContextSourcesRequest, SourcesView>>().Handle(new GetContextSourcesRequest(), ct).ConfigureAwait(false);
        return answer.IsError
            ? McpResults.Failure(services, answer.Error)
            : McpResults.Success(services, McpSourcesAnswer.Of(answer.Value, McpEndpoint.Build, port), McpSummaries.Sources(answer.Value, McpEndpoint.Build, port), page: page.Value);
    }

    /// <summary>The arguments as a request; SND-CONTEXT-022 for an unknown property or a wrong type. Values are left to the engine (006).</summary>
    private static Result<QueryContextRequest> Bind(IDictionary<string, JsonElement>? arguments)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        var numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, value) in arguments ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal))
        {
            if (!BindOne(name, value, texts, numbers))
            {
                return ContextErrors.McpArgumentsInvalid.With(Invalid(name, value));
            }
        }

        return new QueryContextRequest(Get(texts, "q"), Get(texts, "node"), Get(texts, "item"), Get(texts, "symbol"),
            numbers.TryGetValue("depth", out var depth) ? depth.ToString(System.Globalization.CultureInfo.InvariantCulture) : null, numbers.TryGetValue("budget", out var budget) ? budget.ToString(System.Globalization.CultureInfo.InvariantCulture) : null, Diff: Get(texts, "diff"), Hops: Get(texts, "hops"), MapVersion: Get(texts, "mapVersion"));
    }

    /// <summary>One argument into its map; false for an unknown name or a wrong type. An optional argument sent as null is absent.</summary>
    private static bool BindOne(string name, JsonElement value, Dictionary<string, string> texts, Dictionary<string, int> numbers)
    {
        var text = _texts.Contains(name, StringComparer.Ordinal);
        var number = _numbers.Contains(name, StringComparer.Ordinal);
        if (value.ValueKind == JsonValueKind.Null)
        {
            return text || number;
        }

        if (text && value.ValueKind == JsonValueKind.String)
        {
            texts[name] = value.GetString()!;   // why: a String kind always has a string
            return true;
        }

        if (number && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed))
        {
            numbers[name] = parsed;
            return true;
        }

        return false;
    }

    private static string? Get(Dictionary<string, string> texts, string name) => texts.TryGetValue(name, out var text) ? text : null;

    private static string Invalid(string name, JsonElement value) =>
        _texts.Contains(name, StringComparer.Ordinal) || _numbers.Contains(name, StringComparer.Ordinal)
            ? $"'{name}' must be {(_texts.Contains(name, StringComparer.Ordinal) ? "a string" : "an integer")}, got {value.ValueKind.ToString().ToLowerInvariant()} (fix: see the tool's input schema)"
            : $"context_query has no argument '{name}' (fix: q, node, item, symbol, diff, depth, hops, mapVersion, budget, limit, offset)";

    /// <summary>An object schema with these properties and no others.</summary>
    internal static JsonElement Schema(JsonObject properties, string[]? required = null)
    {
        var schema = new JsonObject { ["type"] = "object", ["additionalProperties"] = false, ["properties"] = properties };
        if (required is not null)
        {
            schema["required"] = new JsonArray([.. required.Select(r => (JsonNode?)JsonValue.Create(r))]);
        }

        using var document = JsonDocument.Parse(schema.ToJsonString());
        return document.RootElement.Clone();
    }

    internal static JsonObject Property(string type, string description, int? minimum = null, PropertyBounds? bounds = null)
    {
        var property = new JsonObject { ["type"] = type, ["description"] = description };
        if (minimum is { } min)
        {
            property["minimum"] = min;
        }

        if (bounds?.Allowed is { } allowed)
        {
            property["enum"] = new JsonArray([.. allowed.Select(a => (JsonNode?)JsonValue.Create(a))]);
        }

        if (bounds?.MinLength is { } shortest)
        {
            property["minLength"] = shortest;
        }

        if (bounds?.MaxLength is { } longest)
        {
            property["maxLength"] = longest;
        }

        return property;
    }

    /// <summary>The limit and offset arguments, for a tool whose other properties are none.</summary>
    /// <returns>The page properties.</returns>
    internal static JsonObject PageProperties() => new()
    {
        ["limit"] = Property("integer", "How many items to keep from each list (default 100)", minimum: 1),
        ["offset"] = Property("integer", "How many items to skip in each list (default 0)", minimum: 0),
    };

    /// <summary>The optional limits of a string property.</summary>
    /// <param name="Allowed">The only values allowed, or null.</param>
    /// <param name="MinLength">The fewest characters, or null.</param>
    /// <param name="MaxLength">The most characters, or null.</param>
    internal sealed record PropertyBounds(string[]? Allowed = null, int? MinLength = null, int? MaxLength = null);
}
