using System.Text;
using System.Text.Json.Nodes;
using Aw.Forge.Core.Refs;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Features.ListItems;
using Aw.Sindri.Features.StartInstance;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri pr babysit &lt;url&gt; [--item &lt;id&gt;] [--json]' and 'sindri docs sync &lt;url&gt; [--item &lt;id&gt;] [--json]' (PR babysitter spec API16, §4.2 'The hand start'):
/// start 'workflow.pr-babysitter' or 'workflow.docs-sync' on a pull request by its link, with the facts §4.2 lists (the link as a JSON string, {} for every
/// other declared input) sent as content, never as paths. The link is checked here for its shape only (https, owner/repo/pull/number); the intake Step
/// refuses a host or repo the approved profile doesn't name before any network call. The item is --item, else the open item titled with the link, else a new one titled so.
/// Same caller class as 'workflow start'; exit 0 started or found, 1 not running, 2 invalid, refused or error.
/// </summary>
internal static class HandStartCommand
{
    internal const string BabysitUsage = "usage: sindri pr babysit <url> [--item <id>] [--json]";
    internal const string SyncUsage = "usage: sindri docs sync <url> [--item <id>] [--json]";

    /// <summary>Every declared input a babysitter has beyond the link: the hand start leaves them empty.</summary>
    private static readonly string[] _babysitterEmpty = ["fact:handoff", "fact:phasePacket", "fact:lineContext", "fact:workspace"];

    internal static Task<int> Babysit(string[] args, RecipeDesk desk, IServiceProvider services, Output output, CancellationToken ct) =>
        Run(new Start("workflow.pr-babysitter", _babysitterEmpty, BabysitUsage), args, (desk, services, output), ct);

    internal static Task<int> DocsSync(string[] args, RecipeDesk desk, IServiceProvider services, Output output, CancellationToken ct) =>
        Run(new Start("workflow.docs-sync", ["fact:phasePacket"], SyncUsage), args, (desk, services, output), ct);

    private static async Task<int> Run(Start start, string[] args, (RecipeDesk Desk, IServiceProvider Services, Output Output) wire, CancellationToken ct)
    {
        var parsed = Parse(args, start.Usage);
        if (parsed.IsError)
        {
            return await wire.Output.Error(parsed.Error).ConfigureAwait(false);
        }

        var (link, item, json) = parsed.Value;
        if (item is null)
        {
            var resolved = await Item(link, wire, ct).ConfigureAwait(false);
            if (resolved.IsError)
            {
                return await wire.Output.Error(resolved.Error).ConfigureAwait(false);
            }

            item = resolved.Value;
        }

        return await WorkflowStartCommand.Start(Request(start, link, item), json, wire, ct).ConfigureAwait(false);
    }

    private static Result<(string Link, string? Item, bool Json)> Parse(string[] args, string usage)
    {
        string? url = null;
        string? item = null;
        var json = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--item" when i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal):
                    item = args[++i];
                    break;
                case var flag when flag.StartsWith("--", StringComparison.Ordinal):
                    return SindriErrors.InvalidInput.With($"'{flag}' isn't an option of this command, or it needs a value ({usage})");
                case var word when url is null:
                    url = word;
                    break;
                case var word:
                    return SindriErrors.InvalidInput.With($"'{word}' is a second link ({usage})");
            }
        }

        if (url is null)
        {
            return SindriErrors.InvalidInput.With($"the pull request's link is required ({usage})");
        }

        var canonical = Canonical(url);
        return canonical.IsError ? canonical.Error : (canonical.Value, item, json);
    }

    /// <summary>The link in its canonical form, https://host/owner/repo/pull/number; whether the host is one the profile allows is the intake Step's to refuse.</summary>
    private static Result<string> Canonical(string url)
    {
        const string Fix = "(fix: pass the pull request's link, such as https://github.com/<owner>/<repo>/pull/<number>)";
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return SindriErrors.InvalidInput.With($"'{url}' isn't a pull request link: not an absolute URL {Fix}");
        }

        var parsed = ForgeRef.Parse(url, ForgeKind.GitHub, new ForgeHosts([uri.IdnHost]));
        return parsed.IsError
            ? SindriErrors.InvalidInput.With($"'{url}' isn't a pull request link: {parsed.Error.Message} {Fix}")
            : parsed.Value.Link;
    }

    /// <summary>The open item titled with the link, else a new item titled with it. Needs the control plane, so a human's command may start it, as 'item add' does.</summary>
    private static async Task<Result<string>> Item(string link, (RecipeDesk Desk, IServiceProvider Services, Output Output) wire, CancellationToken ct)
    {
        var desk = wire.Desk;
        if (await desk.EnsureRunning(ct).ConfigureAwait(false) is { } down)
        {
            return down;
        }

        var listed = await desk.Client.GetView<ListItemsResponse>("/sindri/items", ct).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var list = listed.Value.Value;
        if (list.AsOf.Rebuilding || list.AsOf.Stalled)
        {
            return SindriErrors.InvalidInput.With("the item list is rebuilding, so an item for this link can't be told from none (fix: pass --item <id>, or try again in a moment)");
        }

        if (list.Items.FirstOrDefault(i => string.Equals(i.Title, link, StringComparison.Ordinal)) is { } found)
        {
            return found.Id.Value;
        }

        var created = await desk.Client.Post<CreateItemRequest, ItemCreatedResponse>("/sindri/items", new CreateItemRequest(link), ct).ConfigureAwait(false);
        return created.IsError ? created.Error : created.Value.Value.ItemId.Value;
    }

    private static StartInstanceRequest Request(Start start, string link, string item)
    {
        var facts = new Dictionary<FactKey, StartFact>
        {
            [FactKey.From("fact:pr")] = Fact("pr.json", JsonValue.Create(link).ToJsonString()),
        };
        foreach (var key in start.EmptyFacts)
        {
            facts[FactKey.From(key)] = Fact("empty.json", "{}");
        }

        return new StartInstanceRequest(start.Recipe, item, NextAttempt: false, facts);
    }

    private static StartFact Fact(string name, string json) => new(name, Encoding.UTF8.GetBytes(json));

    private sealed record Start(string Recipe, string[] EmptyFacts, string Usage);
}
