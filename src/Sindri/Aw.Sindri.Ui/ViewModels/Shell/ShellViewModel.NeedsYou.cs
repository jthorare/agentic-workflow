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
    private async Task ReadNeedsYou(Uri url, CancellationToken cancellationToken)
    {
        var list = await _api.NeedsYou(url, cancellationToken).ConfigureAwait(true);
        if (list.IsError)
        {
            return;
        }

        var decisions = await _api.Decisions(url, pending: true, item: null, cancellationToken).ConfigureAwait(true);
        var catalog = Health.Catalog.Listed ?? new CommandList([]);
        ShowNeedsYou(NeedsYouMapper.Map(list.Value, catalog, decisions.IsSuccess ? decisions.Value : null, CountsFor(Queue)));
    }

    // Home's other totals are the queue's groups as the queue view (read just before this one) shows them.
    private static HomeCounts CountsFor(QueueViewModel queue)
    {
        int Of(string group) => queue.Groups.Where(g => string.Equals(g.Group, group, StringComparison.Ordinal)).Sum(g => g.Count);
        return new HomeCounts(Running: Of(NeedsYouKinds.Running), WithYou: Of(NeedsYouKinds.WithYou), Queued: Of(NeedsYouKinds.Queued));
    }
}
