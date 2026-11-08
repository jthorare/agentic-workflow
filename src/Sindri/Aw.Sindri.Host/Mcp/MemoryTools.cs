using System.Text.Json;
using System.Text.Json.Nodes;
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

    private static JsonObject Name { get; } = ContextTools.Property("string", "The memory's name, e.g. team-notes");

    private static Tool ListTool { get; } = new()
    {
        Name = List,
        Description = "List Sindri memory stores (named stores of short facts) with their fact counts.",
        InputSchema = ContextTools.Schema(new JsonObject()),
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
    internal static ValueTask<CallToolResult> CallList(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct) => throw new NotSupportedException();

    /// <summary>Runs memory_create.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static ValueTask<CallToolResult> CallCreate(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct) => throw new NotSupportedException();

    /// <summary>Runs memory_search.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static ValueTask<CallToolResult> CallSearch(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct) => throw new NotSupportedException();

    /// <summary>Runs memory_write.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static ValueTask<CallToolResult> CallWrite(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct) => throw new NotSupportedException();

    /// <summary>Runs memory_forget.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static ValueTask<CallToolResult> CallForget(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct) => throw new NotSupportedException();
}
