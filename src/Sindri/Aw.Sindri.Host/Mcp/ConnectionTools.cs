using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// The connection_* tools (connections PR 10a): list, get, add, test, remove. Each is one call to the service its HTTP route calls. Agent sessions may use
/// every one (owner decision D2). No result holds a token, a token prefix or a secret pointer; add for an oauth connection returns the consent URL at once.
/// </summary>
internal static class ConnectionTools
{
    /// <summary>connection_list's name.</summary>
    internal const string List = "connection_list";

    /// <summary>connection_get's name.</summary>
    internal const string Get = "connection_get";

    /// <summary>connection_add's name.</summary>
    internal const string Add = "connection_add";

    /// <summary>connection_test's name.</summary>
    internal const string Test = "connection_test";

    /// <summary>connection_remove's name.</summary>
    internal const string Remove = "connection_remove";

    private static ToolAnnotations ReadOnly { get; } = new() { ReadOnlyHint = true, OpenWorldHint = false };

    private static Tool ListTool { get; } = new()
    {
        Name = List,
        Description = "Every connection: its name, kind, preset, status and whether a secret is stored. Never a token.",
        InputSchema = ContextTools.Schema(ContextTools.PageProperties()),
        Annotations = ReadOnly,
    };

    private static Tool GetTool { get; } = new()
    {
        Name = Get,
        Description = "One connection by name (service:label). Never a token, a token prefix or the secret's location.",
        InputSchema = ContextTools.Schema(new JsonObject { ["name"] = ContextTools.Property("string", "The connection name, e.g. linear:work") }, required: ["name"]),
        Annotations = ReadOnly,
    };

    private static Tool AddTool { get; } = new()
    {
        Name = Add,
        Description = "Add a connection. An oauth connection returns a consent URL to hand to the owner and finishes when they sign in; a rest or cli connection reads the named environment variable once and stores it as the secret.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["name"] = ContextTools.Property("string", "The connection name, e.g. linear:work"),
            ["preset"] = ContextTools.Property("string", "A shipped preset: github, linear, slack, google or spotify"),
            ["kind"] = ContextTools.Property("string", "oauth, mcp, rest or cli; the preset's kind when left out", bounds: new ContextTools.PropertyBounds(Allowed: ["oauth", "mcp", "rest", "cli"])),
            ["clientId"] = ContextTools.Property("string", "The OAuth client id (not secret); presets ship none"),
            ["keyEnv"] = ContextTools.Property("string", "The environment variable that holds the key of a rest or cli connection"),
        }, required: ["name"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = false, IdempotentHint = false, OpenWorldHint = true },
    };

    private static Tool TestTool { get; } = new()
    {
        Name = Test,
        Description = "Test a connection: call its probe URL with the stored credential and report whether the provider accepted it.",
        InputSchema = ContextTools.Schema(new JsonObject { ["name"] = ContextTools.Property("string", "The connection name") }, required: ["name"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = false, IdempotentHint = true, OpenWorldHint = true },
    };

    private static Tool RemoveTool { get; } = new()
    {
        Name = Remove,
        Description = "Remove a connection and delete its stored secret.",
        InputSchema = ContextTools.Schema(new JsonObject { ["name"] = ContextTools.Property("string", "The connection name") }, required: ["name"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = true, IdempotentHint = true, OpenWorldHint = false },
    };

    /// <summary>The five tools.</summary>
    internal static IReadOnlyList<Tool> All { get; } = [ListTool, GetTool, AddTool, TestTool, RemoveTool];

    /// <summary>Runs connection_list.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static ValueTask<CallToolResult> CallList(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct) => throw new NotSupportedException();

    /// <summary>Runs connection_get.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static ValueTask<CallToolResult> CallGet(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct) => throw new NotSupportedException();

    /// <summary>Runs connection_add.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static ValueTask<CallToolResult> CallAdd(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct) => throw new NotSupportedException();

    /// <summary>Runs connection_test.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static ValueTask<CallToolResult> CallTest(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct) => throw new NotSupportedException();

    /// <summary>Runs connection_remove.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static ValueTask<CallToolResult> CallRemove(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct) => throw new NotSupportedException();
}
