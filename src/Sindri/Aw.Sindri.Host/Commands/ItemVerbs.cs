using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Features.GetItem;
using Aw.Sindri.Features.GetQueue;
using Aw.Sindri.Features.ListItems;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri queue', 'items', 'item add' and 'item close' (API P3 unit H, plan G17): clients of the item routes. Human verbs
/// start the control plane unless --no-start; inside an agent session they never do (API spec §7). 'queue' and 'items' exit 1 while the
/// views rebuild or stall, so a script doesn't read a partial queue as the truth (API spec §3.6).
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class ItemVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var parsed = ItemVerbArguments.Parse(args);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var command = parsed.Value;
        var down = ensureRunning is null ? null : await ensureRunning(!command.NoStart, ct).ConfigureAwait(false);
        if (down is not null)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        return command.Verb switch
        {
            ItemVerbArguments.Verb.Queue => await Queue(command, ct).ConfigureAwait(false),
            ItemVerbArguments.Verb.Items => await Items(command, ct).ConfigureAwait(false),
            ItemVerbArguments.Verb.Add => await Add(command, ct).ConfigureAwait(false),
            ItemVerbArguments.Verb.Close => await Close(command, ct).ConfigureAwait(false),
            ItemVerbArguments.Verb.None => await output.Error(SindriErrors.UnknownVerb.With(ItemVerbArguments.Usage)).ConfigureAwait(false),
            _ => await output.Error(SindriErrors.UnknownVerb.With(ItemVerbArguments.Usage)).ConfigureAwait(false),
        };
    }

    private async Task<int> Queue(ItemVerbArguments.Parsed command, CancellationToken ct)
    {
        var answer = await client.GetView<QueueResponse>("/sindri/queue", ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var queue = answer.Value.Value;
        var code = queue.AsOf.Rebuilding || queue.AsOf.Stalled ? 1 : 0;
        return command.Json
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(Render(queue), code).ConfigureAwait(false);
    }

    private async Task<int> Items(ItemVerbArguments.Parsed command, CancellationToken ct)
    {
        var query = UrlQuery.Of([("state", command.State), ("repo", command.Repo)]);

        var answer = await client.GetView<ListItemsResponse>("/sindri/items" + query, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var list = answer.Value.Value;
        var code = list.AsOf.Rebuilding || list.AsOf.Stalled ? 1 : 0;
        return command.Json
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(RenderList(list), code).ConfigureAwait(false);
    }

    private async Task<int> Add(ItemVerbArguments.Parsed command, CancellationToken ct)
    {
        RepoName? repo = null;
        if (command.Repo is { } text)
        {
            if (!RepoName.TryFrom(text, out var name))
            {
                return await output.Error(SindriErrors.InvalidInput.With($"'{text}' isn't a repo name")).ConfigureAwait(false);
            }

            repo = name;
        }

        var created = await client.Post<CreateItemRequest, ItemCreatedResponse>("/sindri/items", new CreateItemRequest(command.Text, repo), ct).ConfigureAwait(false);
        return created.IsError
            ? await output.Error(created.Error).ConfigureAwait(false)
            : await output.Text(string.Create(CultureInfo.InvariantCulture, $"{created.Value.Value.ItemId.Value} created (sequence {created.Value.Value.Accepted.Sequence})\n"), 0).ConfigureAwait(false);
    }

    private async Task<int> Close(ItemVerbArguments.Parsed command, CancellationToken ct)
    {
        var route = "/sindri/items/" + Uri.EscapeDataString(command.Text);
        var version = command.Version;
        if (version is null)
        {
            var read = await client.GetView<ItemView>(route, ct).ConfigureAwait(false);   // the API always wants a version: read the one the human would have seen (G12)
            if (read.IsError)
            {
                return await output.Error(read.Error).ConfigureAwait(false);
            }

            version = read.Value.Value.Version;
        }

        var reason = command.Cancelled ? CloseReason.Cancelled : CloseReason.Done;
        var closed = await client.Post<CloseItemBody, CommandAccepted>(route + "/close", new CloseItemBody(reason, version.Value), ct).ConfigureAwait(false);
        return closed.IsError
            ? await output.Error(closed.Error).ConfigureAwait(false)
            : await output.Text(string.Create(CultureInfo.InvariantCulture, $"{command.Text} closed: {(command.Cancelled ? "cancelled" : "done")} (sequence {closed.Value.Value.Sequence})\n"), 0).ConfigureAwait(false);
    }

    private static string RenderList(ListItemsResponse list)
    {
        var text = new StringBuilder();
        if (list.Empty is { } sentence)
        {
            _ = text.AppendLine(sentence);   // on stale views the rows may be partial, and this says why
        }
        else if (list.Items.Count == 0)
        {
            _ = text.AppendLine(EmptyText.Items(state: null, repo: null));
        }

        return text.Append(Lines(list.Items, indent: string.Empty)).ToString();
    }

    private static string Render(QueueResponse queue)
    {
        var text = new StringBuilder();
        if (queue.Empty is { } sentence)
        {
            _ = text.AppendLine(sentence);
        }

        foreach (var group in queue.Groups)
        {
            _ = text.AppendLine(GroupName(group.Group));
            _ = group.Items.Count > 0
                ? text.Append(Lines(group.Items, indent: "  "))
                : group.Empty is { } said && said == queue.Empty ? text : text.Append("  ").AppendLine(group.Empty ?? EmptyText.Group(group.Group));   // the stall sentence is said once, at the top
        }

        return text.ToString();
    }

    private static string Lines(IReadOnlyList<ItemSummary> items, string indent)
    {
        var text = new StringBuilder();
        foreach (var item in items)
        {
            var repo = item.Repo is { } r ? $"  [{r.Value}]" : string.Empty;
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{indent}{item.Id.Value}  {item.Title}{repo}  {ItemStateNames.Of(item.State)}  since {item.Since.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}"));
        }

        return text.ToString();
    }

    private static string GroupName(QueueGroup group) => group switch
    {
        QueueGroup.WaitingOnYou => "waitingOnYou",
        QueueGroup.WithYou => "withYou",
        QueueGroup.Parked => "parked",
        QueueGroup.Running => "running",
        QueueGroup.Queued => "queued",
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "not a queue group"),
    };
}
