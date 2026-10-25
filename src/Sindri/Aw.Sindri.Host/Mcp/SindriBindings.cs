using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Features.FileRequest;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Mcp;

/// <summary>Binds the arguments of the sindri_* tools that take ids or a request: the shape here, what a value means in the route's own rules (so the codes match HTTP).</summary>
internal static class SindriBindings
{
    internal static Result<FileRequestRequest> Request(IDictionary<string, JsonElement>? arguments, JsonTypeInfo<RequestKind> kinds)
    {
        if (McpArguments.Only(SindriTools.Request, arguments, ["kind", "text", "itemId"]) is { } refused)
        {
            return refused;
        }

        var kind = McpArguments.RequiredEnum(SindriTools.Request, arguments, "kind", kinds);
        var text = McpArguments.RequiredString(SindriTools.Request, arguments, "text", "Split this into two items.");
        var item = McpArguments.OptionalString(SindriTools.Request, arguments, "itemId").Bind(GivenItem);
        return kind.Bind(k => text.Bind(t => item.Map(i => new FileRequestRequest(k, t, i))));
    }

    internal static Result<ItemRef> Item(string tool, IDictionary<string, JsonElement>? arguments, params string[] more) =>
        McpArguments.Only(tool, arguments, ["itemId", .. more, .. McpArguments.PageNames]) is { } refused
            ? refused
            : McpArguments.RequiredString(tool, arguments, "itemId", "ITM-...").Bind(ItemRoute.Item);

    internal static Result<ItemRef?> OptionalItem(string tool, IDictionary<string, JsonElement>? arguments)
    {
        if (McpArguments.Only(tool, arguments, ["itemId", .. McpArguments.PageNames]) is { } refused)
        {
            return refused;
        }

        var text = McpArguments.OptionalString(tool, arguments, "itemId");
        return text.Bind(GivenItem);
    }

    // why: ItemRoute.ItemFilter reads "" as no filter (an empty '?item='); an itemId a caller wrote out is an id or a refusal, never a wider call
    private static Result<ItemRef?> GivenItem(string? text)
    {
        if (text is null)
        {
            return (ItemRef?)null;
        }

        var item = ItemRoute.Item(text);
        return item.IsError ? item.Error : (ItemRef?)item.Value;
    }
}
