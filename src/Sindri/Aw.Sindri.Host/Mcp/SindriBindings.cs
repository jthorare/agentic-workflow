using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aw.Context.Core;
using Aw.Kernel;
using Aw.Sindri.Core.Handoff;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Features.CreateHandoffItem;
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

    /// <summary>The arguments of sindri_handoff as a request: the shape here, the rules (source name, sizes) in the validator the command chain runs.</summary>
    /// <param name="arguments">The call's arguments.</param>
    /// <returns>The request, or the refusal.</returns>
    internal static Result<CreateHandoffItemRequest> Handoff(IDictionary<string, JsonElement>? arguments)
    {
        const string tool = SindriTools.Handoff;
        if (McpArguments.Only(tool, arguments, ["title", "source", "repo", "branch", "prompt", "offerKey", "links", "labels"]) is { } refused)
        {
            return refused;
        }

        var lists = Links(tool, arguments).Bind(links => Strings(tool, arguments, "labels").Map(labels => (links, labels)));
        var repo = McpArguments.OptionalString(tool, arguments, "repo").Bind(RepoOf);
        return McpArguments.RequiredString(tool, arguments, "title", "Fix the login timeout").Bind(title =>
            McpArguments.OptionalString(tool, arguments, "source").Bind(source =>
            McpArguments.OptionalString(tool, arguments, "branch").Bind(branch =>
            McpArguments.OptionalString(tool, arguments, "prompt").Bind(prompt =>
            McpArguments.OptionalString(tool, arguments, "offerKey").Bind(offer =>
            repo.Bind(named => lists.Map(l => new CreateHandoffItemRequest(title, source ?? "mcp", offer, named, branch, prompt, l.links, l.labels))))))));
    }

    private static Result<RepoName?> RepoOf(string? text)
    {
        if (text is null)
        {
            return (RepoName?)null;
        }

        return RepoName.TryFrom(text, out var name) ? name : ContextErrors.McpArgumentsInvalid.With($"'{text}' isn't a repo name (fix: name a repo of the profile)");
    }

    // why: the inbox offer's link shape ({label, url}); the validator checks the url, this checks only that both are strings
    private static Result<HandoffLink[]?> Links(string tool, IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null || !arguments.TryGetValue("links", out var raw) || raw.ValueKind == JsonValueKind.Null)
        {
            return (HandoffLink[]?)null;
        }

        if (raw.ValueKind != JsonValueKind.Array || raw.EnumerateArray().Any(static e => !IsLink(e)))
        {
            return ContextErrors.McpArgumentsInvalid.With($"'links' must be an array of {{label, url}} objects (fix: see {tool}'s input schema)");
        }

        HandoffLink[] links = [.. raw.EnumerateArray().Select(static e => new HandoffLink(e.GetProperty("label").GetString()!, e.GetProperty("url").GetString()!))];   // why: IsLink checked both
        return links;
    }

    private static bool IsLink(JsonElement e) =>
        e.ValueKind == JsonValueKind.Object
        && e.TryGetProperty("label", out var label) && label.ValueKind == JsonValueKind.String
        && e.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String
        && e.EnumerateObject().Take(3).Count() == 2;

    private static Result<string[]?> Strings(string tool, IDictionary<string, JsonElement>? arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var raw) || raw.ValueKind == JsonValueKind.Null)
        {
            return (string[]?)null;
        }

        if (raw.ValueKind != JsonValueKind.Array || raw.EnumerateArray().Any(static e => e.ValueKind != JsonValueKind.String))
        {
            return ContextErrors.McpArgumentsInvalid.With($"'{name}' must be an array of strings (fix: see {tool}'s input schema)");
        }

        string[] texts = [.. raw.EnumerateArray().Select(static e => e.GetString()!)];   // why: every element is a String kind
        return texts;
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
