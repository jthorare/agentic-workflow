using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>The Closed fold under the Work table (<see cref="QueueViewModel"/>, continued): done and cancelled items, read on demand, once.</summary>
internal sealed partial class QueueViewModel
{
    private bool _closedRead;

    /// <summary>The API the last refresh read from; the fold reads through it.</summary>
    internal Uri? LastUrl { get; private set; }

    /// <summary>True while the fold is open.</summary>
    public bool IsClosedOpen { get; private set => Set(ref field, value); }

    /// <summary>"Closed" before the first read, then "Closed (3)".</summary>
    public string ClosedHeader { get; private set => Set(ref field, value); } = Labels.ClosedHeader(count: null);

    /// <summary>The closed items, newest first; they carry no actions.</summary>
    public ObservableCollection<ItemRowViewModel> ClosedRows { get; } = [];

    /// <summary>Opens or closes the fold; the first opening reads done and cancelled.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the fold shows.</returns>
    internal async Task ToggleClosed(Uri url, CancellationToken cancellationToken)
    {
        IsClosedOpen = !IsClosedOpen;
        if (!IsClosedOpen || _closedRead)
        {
            return;
        }

        var done = await api.ListItems(url, "done", cancellationToken).ConfigureAwait(true);
        var cancelled = await api.ListItems(url, "cancelled", cancellationToken).ConfigureAwait(true);
        if (done.IsError && cancelled.IsError)
        {
            return;   // nothing was read: the next opening tries again
        }

        _closedRead = true;
        var now = clock.GetUtcNow();
        var items = (done.IsSuccess ? done.Value.Items : []).Concat(cancelled.IsSuccess ? cancelled.Value.Items : []).OrderByDescending(i => i.Since);
        Patch.To(ClosedRows, [.. items.Select(i => new ItemRowViewModel(i, "closed", primaryAction: null, now))]);
        ClosedHeader = Labels.ClosedHeader(ClosedRows.Count);
    }
}
