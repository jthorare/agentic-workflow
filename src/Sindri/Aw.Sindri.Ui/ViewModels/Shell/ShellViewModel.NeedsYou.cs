using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The shell's reading of what needs the human (<see cref="ShellViewModel"/>, continued): one re-read on every event, whatever page is visible.</summary>
internal sealed partial class ShellViewModel
{
    private readonly ISindriApi _api;

    // Needs-you and the pending decisions (whose options become the rows' Decide buttons) are read together. The rows
    // show on a failed read as they were: the attention summary keeps its last list, and the next event tries again.
    // A failed FIRST read says so (a failed line and Retry) instead of leaving the skeleton up.
    private async Task ReadNeedsYou(Uri url, CancellationToken cancellationToken)
    {
        var list = await _api.NeedsYou(url, cancellationToken).ConfigureAwait(true);
        if (list.IsError)
        {
            NeedsYou.ReadFailed(Labels.NeedsYouReadFailed);
            return;
        }

        var decisions = await _api.Decisions(url, pending: true, item: null, cancellationToken).ConfigureAwait(true);
        var catalog = Health.Catalog.Listed ?? new CommandList([]);
        ShowNeedsYou(NeedsYouMapper.Map(list.Value, catalog, decisions.IsSuccess ? decisions.Value : null, CountsFor(Queue)));
    }

    /// <summary>Reads the list again, for the Retry button; does nothing while the API's address is unknown.</summary>
    internal async Task RetryNeedsYou()
    {
        if (Health.ApiUrl() is { } url)
        {
            await ReadNeedsYou(url, CancellationToken.None).ConfigureAwait(true);
        }
    }

    // The queue's read, then Home's and the tray's totals again from its groups. Needs-you is read at the same time (#2775), so
    // its answer may come before the queue's and map the groups of the queue read before: this maps them again once the queue has answered.
    private async Task ReadQueue(Uri url, CancellationToken cancellationToken)
    {
        await Queue.Refresh(url, cancellationToken).ConfigureAwait(true);
        Recount();
    }

    // The last needs-you list with the totals the queue shows now; nothing changes when they are the ones it already has.
    private void Recount()
    {
        var counts = CountsFor(Queue);
        if (_needsYou is not { } feed || feed.Counts == counts)
        {
            return;
        }

        _needsYou = feed with { Counts = counts };
        UpdateNeedsYou();
        Reattend();
    }

    // Home's other totals are the queue's groups as the queue view shows them now; ReadQueue maps them again when the queue answers later.
    private static HomeCounts CountsFor(QueueViewModel queue)
    {
        int Of(string group) => queue.Groups.Where(g => string.Equals(g.Group, group, StringComparison.Ordinal)).Sum(g => g.Count);
        return new HomeCounts(Running: Of(NeedsYouKinds.Running), WithYou: Of(NeedsYouKinds.WithYou), Queued: Of(NeedsYouKinds.Queued));
    }
}
