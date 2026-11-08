using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.GetConnection;
using Aw.Sindri.Features.ListConnections;
using Aw.Sindri.Features.RemoveConnection;
using Aw.Sindri.Features.TestConnection;
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
        Description = "Add a connection. An oauth connection returns a consent URL to hand to the owner and finishes when they sign in; a rest, cli or mcp connection with keyEnv reads the named environment variable once and stores it as the secret; an mcp connection also needs url.",
        InputSchema = ContextTools.Schema(new JsonObject
        {
            ["name"] = ContextTools.Property("string", "The connection name, e.g. linear:work"),
            ["preset"] = ContextTools.Property("string", "A shipped preset: github, linear, slack, google or spotify"),
            ["kind"] = ContextTools.Property("string", "oauth, mcp, rest or cli; the preset's kind when left out", bounds: new ContextTools.PropertyBounds(Allowed: ["oauth", "mcp", "rest", "cli"])),
            ["clientId"] = ContextTools.Property("string", "The OAuth client id (not secret); presets ship none"),
            ["keyEnv"] = ContextTools.Property("string", "The NAME of an environment variable in the environment of the running sindri serve (not your shell) that holds the key of a rest or cli connection; the error says when serve has no such variable"),
            ["clientSecretEnv"] = ContextTools.Property("string", "The NAME of an environment variable in the running sindri serve's environment that holds the OAuth client secret, for providers that want one (the github and google presets)"),
            ["url"] = ContextTools.Property("string", "The MCP server's address, for kind mcp (https, or http on this machine). With keyEnv it connects with that key; without one it starts the server's own sign-in and returns a consent URL"),
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
    internal static async ValueTask<CallToolResult> CallList(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        if (McpArguments.Only(List, arguments, [.. McpArguments.PageNames]) is { } refused)
        {
            return McpResults.Failure(services, refused);
        }

        var page = McpArguments.ReadPage(arguments);
        if (page.IsError)
        {
            return McpResults.Failure(services, page.Error);
        }

        var answer = await services.GetRequiredService<IService<ListConnectionsRequest, ListConnectionsResponse>>().Handle(new ListConnectionsRequest(), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static listed => string.Create(CultureInfo.InvariantCulture, $"{listed.Connections.Count} connections"), page: page.Value);
    }

    /// <summary>Runs connection_get.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallGet(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var name = Name(Get, arguments);
        if (name.IsError)
        {
            return McpResults.Failure(services, name.Error);
        }

        var answer = await services.GetRequiredService<IService<GetConnectionRequest, GetConnectionResponse>>().Handle(new GetConnectionRequest(name.Value), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static got => Summary(got.Connection));
    }

    /// <summary>Runs connection_add.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallAdd(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var request = BindAdd(arguments);
        if (request.IsError)
        {
            return McpResults.Failure(services, request.Error);
        }

        var answer = await services.GetRequiredService<IService<AddConnectionRequest, AddConnectionResponse>>().Handle(request.Value, ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static added => added.ConsentUrl is null ? Summary(added.Connection) : "Sign-in started. Give the consentUrl to the owner; the connection finishes when they sign in.");
    }

    /// <summary>Runs connection_test.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallTest(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var name = Name(Test, arguments);
        if (name.IsError)
        {
            return McpResults.Failure(services, name.Error);
        }

        var answer = await services.GetRequiredService<IService<TestConnectionRequest, TestConnectionResponse>>().Handle(new TestConnectionRequest(name.Value), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static tested => tested.Ok ? "The provider accepted the credential." : "The provider did not accept the credential; see reason.");
    }

    /// <summary>Runs connection_remove.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallRemove(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var name = Name(Remove, arguments);
        if (name.IsError)
        {
            return McpResults.Failure(services, name.Error);
        }

        var answer = await services.GetRequiredService<IService<RemoveConnectionRequest, RemoveConnectionResponse>>().Handle(new RemoveConnectionRequest(name.Value), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static removed => removed.SecretDeleted ? "Removed the connection and its secret." : "Removed the connection (it had no stored secret).");
    }

    private static readonly string[] _optionalAdd = ["preset", "kind", "clientId", "keyEnv", "clientSecretEnv", "url"];

    // why: an agent cannot wait minutes for a browser, so Wait is false: it gets the consent URL at once and the sign-in finishes in the background. The key and the client secret are
    // NAMES of variables read from the running sindri serve's environment: an MCP call has no shell of its own.
    private static Result<AddConnectionRequest> BindAdd(IDictionary<string, JsonElement>? arguments)
    {
        if (McpArguments.Only(Add, arguments, "name", "preset", "kind", "clientId", "keyEnv", "clientSecretEnv", "url") is { } refused)
        {
            return refused;
        }

        var name = McpArguments.RequiredString(Add, arguments, "name", "linear:work");
        var optional = _optionalAdd.Select(n => McpArguments.OptionalString(Add, arguments, n)).ToList();
        if (name.IsError)
        {
            return name.Error;
        }

        var bad = optional.FirstOrDefault(static o => o.IsError);
        if (bad is not null)
        {
            return bad.Error;
        }

        return new AddConnectionRequest(name.Value, optional[0].Value, optional[1].Value, optional[2].Value, optional[3].Value, [], Wait: false, ClientSecretEnv: optional[4].Value, Url: optional[5].Value);
    }

    private static Result<string> Name(string tool, IDictionary<string, JsonElement>? arguments) =>
        McpArguments.Only(tool, arguments, "name") is { } refused ? refused : McpArguments.RequiredString(tool, arguments, "name", "linear:work");

    private static string Summary(ConnectionView view) => $"{view.Name} ({view.Kind}): {view.Status}";
}
