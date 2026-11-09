using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ListResources;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// The resources_* tools (agent-first PR 14): resources_list shows what a workflow, node or toolkit had provisioned (owner, kind, name, size, path).
/// It is one call to the service its HTTP route calls. The purge stays on the HTTP route and the CLI: a human decides.
/// </summary>
internal static class ResourceTools
{
    /// <summary>resources_list's name.</summary>
    internal const string List = "resources_list";

    private const string OwnerText = "An owner: workflow:<slug>, node:<slug>/<id> or toolkit:<name>";

    private static Tool ListTool { get; } = new()
    {
        Name = List,
        Description = "List the databases and disks Sindri provisioned for workflows, nodes and toolkits: owner, kind, name, size in bytes and path. Give owner to list one owner's.",
        InputSchema = ContextTools.Schema(new JsonObject { ["owner"] = ContextTools.Property("string", OwnerText) }),
        Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = false },
    };

    /// <summary>The tool.</summary>
    internal static IReadOnlyList<Tool> All { get; } = [ListTool];

    /// <summary>Runs resources_list.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static ValueTask<CallToolResult> Call(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct) =>
        CallList(services, arguments, ct);

    private static async ValueTask<CallToolResult> CallList(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var refused = McpArguments.Only(List, arguments, "owner");
        var owner = McpArguments.OptionalString(List, arguments, "owner");
        if ((refused ?? (owner.IsError ? owner.Error : null)) is { } error)
        {
            return McpResults.Failure(services, error);
        }

        var answer = await services.GetRequiredService<IService<ListResourcesRequest, ListResourcesResponse>>().Handle(new ListResourcesRequest(owner.Value), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static a => string.Create(CultureInfo.InvariantCulture, $"{a.Resources.Count} resources"));
    }
}
