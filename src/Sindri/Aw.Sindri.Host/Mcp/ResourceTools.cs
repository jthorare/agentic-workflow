using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ListResources;
using Aw.Sindri.Features.PurgeResources;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// The resources_* tools (agent-first PR 14): resources_list shows what a workflow, node or toolkit had provisioned (owner, kind, name, size, path) and resources_purge deletes one owner's.
/// Each is one call to the service its HTTP route calls. A purge is refused while the owner's workflow is active or has a run in flight.
/// </summary>
internal static class ResourceTools
{
    /// <summary>resources_list's name.</summary>
    internal const string List = "resources_list";

    /// <summary>resources_purge's name.</summary>
    internal const string Purge = "resources_purge";

    private const string OwnerText = "An owner: workflow:<slug>, node:<slug>/<id> or toolkit:<name>";

    private static Tool ListTool { get; } = new()
    {
        Name = List,
        Description = "List the databases and disks Sindri provisioned for workflows, nodes and toolkits: owner, kind, name, size in bytes and path. Give owner to list one owner's.",
        InputSchema = ContextTools.Schema(new JsonObject { ["owner"] = ContextTools.Property("string", OwnerText) }),
        Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = false },
    };

    private static Tool PurgeTool { get; } = new()
    {
        Name = Purge,
        Description = "Delete every database and disk of one owner for good. Refused while the owner's workflow is active or has a run in flight; archiving a workflow keeps its data.",
        InputSchema = ContextTools.Schema(new JsonObject { ["owner"] = ContextTools.Property("string", OwnerText) }, required: ["owner"]),
        Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = true, IdempotentHint = true, OpenWorldHint = false },
    };

    /// <summary>The two tools.</summary>
    internal static IReadOnlyList<Tool> All { get; } = [ListTool, PurgeTool];

    /// <summary>Runs the resource tool named <paramref name="tool"/>.</summary>
    /// <param name="tool">The tool's name.</param>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static ValueTask<CallToolResult> Call(string tool, IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct) =>
        string.Equals(tool, List, StringComparison.Ordinal) ? CallList(services, arguments, ct) : CallPurge(services, arguments, ct);

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

    private static async ValueTask<CallToolResult> CallPurge(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var refused = McpArguments.Only(Purge, arguments, "owner");
        var owner = McpArguments.RequiredString(Purge, arguments, "owner", "workflow:morning-check");
        if ((refused ?? (owner.IsError ? owner.Error : null)) is { } error)
        {
            return McpResults.Failure(services, error);
        }

        var answer = await services.GetRequiredService<IService<PurgeResourcesRequest, PurgeResourcesResponse>>().Handle(new PurgeResourcesRequest(owner.Value), ct).ConfigureAwait(false);
        return McpResults.From(services, answer, static a => string.Create(CultureInfo.InvariantCulture, $"purged {a.Removed} resources of {a.Owner}"));
    }
}
