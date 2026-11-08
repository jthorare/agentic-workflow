using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Features.CreateMemory;
using Aw.Sindri.Features.ForgetMemoryFact;
using Aw.Sindri.Features.ListMemories;
using Aw.Sindri.Features.SearchMemory;
using Aw.Sindri.Features.WriteMemoryFact;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// The memory_* tools (agent-first PR 13): list, create, search, write, forget Sindri memory stores. Each is one call to the service its HTTP route calls, and an owner's
/// own session gets the same tools through /sindri/mcp. The descriptions say "Sindri memory stores" so a model does not pick another server's memory_search.
/// </summary>
internal static class MemoryTools
{
    /// <summary>memory_list's name.</summary>
    internal const string List = "memory_list";

    /// <summary>memory_create's name.</summary>
    internal const string Create = "memory_create";

    /// <summary>memory_search's name.</summary>
    internal const string Search = "memory_search";

    /// <summary>memory_write's name.</summary>
    internal const string Write = "memory_write";

    /// <summary>memory_forget's name.</summary>
    internal const string Forget = "memory_forget";

    private static JsonObject Name => ContextTools.Property("string", "The memory's name, e.g. team-notes");

    private static Tool ListTool { get; } = new()
    {
        Name = List,
        Description = "List Sindri memory stores (named stores of short facts) with their fact counts.",
        InputSchema = ContextTools.Schema([]),
        Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = false },
    };

    private static Tool CreateTool { get; } = new()
    {
        Name = Create,
        Description = "Make a Sindri memory store: a named place to keep short facts that later runs can recall by meaning.",
        InputSchema = ContextTools.Schema(new JsonObject { ["name"] = Name, ["description"] = ContextTools.Property("string", "What the store is for") }, required: ["name"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = false, IdempotentHint = false, OpenWorldHint = false },
    };

    private static Tool SearchTool { get; } = new()
    {
        Name = Search,
        Description = "Recall facts from a Sindri memory store by meaning (keyword match when the local embedding model is off; the answer's mode says which). Returns facts with score and provenance.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["name"] = Name,
            ["query"] = ContextTools.Property("string", "What to recall"),
            ["level"] = ContextTools.Property("string", "How close a fact must be: loose, balanced (default) or strict", bounds: new ContextTools.PropertyBounds(Allowed: ["loose", "balanced", "strict"])),
            ["limit"] = ContextTools.Property("integer", "At most this many facts (default 10, most 50)", minimum: 1),
        }, required: ["name", "query"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = false },
    };

    private static Tool WriteTool { get; } = new()
    {
        Name = Write,
        Description = "Write a fact (1 to 2,000 characters) to a Sindri memory store. A near duplicate with the same tags refreshes the old fact instead of adding one. Secrets are scrubbed before storing.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["name"] = Name,
            ["text"] = ContextTools.Property("string", "The fact"),
            ["tags"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" }, ["description"] = "Tags; part of the fact's identity" },
            ["expiresAt"] = ContextTools.Property("string", "ISO 8601 time after which the fact is deleted"),
        }, required: ["name", "text"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = false, IdempotentHint = true, OpenWorldHint = false },
    };

    private static Tool ForgetTool { get; } = new()
    {
        Name = Forget,
        Description = "Forget facts in a Sindri memory store: one by id, every one with a tag, or every expired one. Give exactly one of fact, tag or expired.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["name"] = Name,
            ["fact"] = ContextTools.Property("string", "One fact's id"),
            ["tag"] = ContextTools.Property("string", "Every fact with this tag"),
            ["expired"] = ContextTools.Property("boolean", "True: every expired fact"),
        }, required: ["name"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = true, IdempotentHint = true, OpenWorldHint = false },
    };

    /// <summary>The five tools.</summary>
    internal static IReadOnlyList<Tool> All { get; } = [ListTool, CreateTool, SearchTool, WriteTool, ForgetTool];

    /// <summary>Runs memory_list.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallList(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        if (McpArguments.None(List, arguments) is { } refused)
        {
            return McpResults.Failure(services, refused);
        }

        var answer = await services.GetRequiredService<IService<ListMemoriesRequest, ListMemoriesResponse>>().Handle(new ListMemoriesRequest(), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static a => $"{a.Memories.Count.ToString(CultureInfo.InvariantCulture)} Sindri memory stores");
    }

    /// <summary>Runs memory_create.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallCreate(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var refused = McpArguments.Only(Create, arguments, "name", "description");
        var required = McpArguments.RequiredString(Create, arguments, "name", "team-notes");
        var description = McpArguments.OptionalString(Create, arguments, "description");
        if ((refused ?? required.E() ?? description.E()) is { } error)
        {
            return McpResults.Failure(services, error);
        }

        var answer = await services.GetRequiredService<IService<CreateMemoryRequest, CreateMemoryResponse>>().Handle(new CreateMemoryRequest(required.Value, description.Value), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static a => $"made Sindri memory store {a.Store.Name}");
    }

    /// <summary>Runs memory_search.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallSearch(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var refused = McpArguments.Only(Search, arguments, "name", "query", "level", "limit");
        var name = McpArguments.RequiredString(Search, arguments, "name", "team-notes");
        var query = McpArguments.RequiredString(Search, arguments, "query", "how do we deploy");
        var level = McpArguments.OptionalString(Search, arguments, "level");
        var limit = McpArguments.OptionalLong(Search, arguments, "limit", min: 1);
        if ((refused ?? name.E() ?? query.E() ?? level.E() ?? limit.E()) is { } error)
        {
            return McpResults.Failure(services, error);
        }

        var request = new SearchMemoryRequest(name.Value, query.Value, level.Value, limit.Value is { } n ? (int)Math.Min(n, int.MaxValue) : null);
        var answer = await services.GetRequiredService<IService<SearchMemoryRequest, SearchMemoryResponse>>().Handle(request, ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static a => $"{a.Count.ToString(CultureInfo.InvariantCulture)} facts from Sindri memory, mode {a.Mode}");
    }

    /// <summary>Runs memory_write.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallWrite(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var refused = McpArguments.Only(Write, arguments, "name", "text", "tags", "expiresAt");
        var name = McpArguments.RequiredString(Write, arguments, "name", "team-notes");
        var text = McpArguments.RequiredString(Write, arguments, "text", "Deploys run from main");
        var expires = McpArguments.OptionalString(Write, arguments, "expiresAt");
        var tags = Tags(arguments);
        if ((refused ?? name.E() ?? text.E() ?? expires.E() ?? tags.E()) is { } error)
        {
            return McpResults.Failure(services, error);
        }

        DateTimeOffset? at = null;
        if (expires.Value is { Length: > 0 } stamp)
        {
            if (!DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return McpResults.Failure(services, ContextErrors.McpArgumentsInvalid.With("'expiresAt' must be an ISO 8601 time (fix: 2026-12-01T00:00:00Z)"));
            }

            at = parsed;
        }

        var answer = await services.GetRequiredService<IService<WriteMemoryFactRequest, WriteMemoryFactResponse>>()
            .Handle(new WriteMemoryFactRequest(name.Value, text.Value, tags.Value, at, Provenance: null), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static a => $"{a.Action} fact {a.Key}");
    }

    /// <summary>Runs memory_forget.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallForget(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var refused = McpArguments.Only(Forget, arguments, "name", "fact", "tag", "expired");
        var name = McpArguments.RequiredString(Forget, arguments, "name", "team-notes");
        var fact = McpArguments.OptionalString(Forget, arguments, "fact");
        var tag = McpArguments.OptionalString(Forget, arguments, "tag");
        var expired = McpArguments.OptionalBool(Forget, arguments, "expired");
        if ((refused ?? name.E() ?? fact.E() ?? tag.E() ?? expired.E()) is { } error)
        {
            return McpResults.Failure(services, error);
        }

        var answer = await services.GetRequiredService<IService<ForgetMemoryFactRequest, ForgetMemoryFactResponse>>()
            .Handle(new ForgetMemoryFactRequest(name.Value, fact.Value, tag.Value, expired.Value == true), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static a => $"forgot {a.Deleted.ToString(CultureInfo.InvariantCulture)} facts");
    }

    private static Result<IReadOnlyList<string>?> Tags(IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null || !arguments.TryGetValue("tags", out var raw) || raw.ValueKind == JsonValueKind.Null)
        {
            return Aw.Kernel.Results.Success<IReadOnlyList<string>?>(value: null);
        }

        return raw.ValueKind == JsonValueKind.Array && raw.EnumerateArray().All(static t => t.ValueKind == JsonValueKind.String)
            ? Aw.Kernel.Results.Success<IReadOnlyList<string>?>(value: [.. raw.EnumerateArray().Select(static t => t.GetString() ?? string.Empty)])
            : ContextErrors.McpArgumentsInvalid.With("'tags' must be an array of strings (fix: see memory_write's input schema)");
    }

    private static Error? E<T>(this Result<T> result) => result.IsError ? result.Error : null;
}
