using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>The Closed fold under the Work table (<see cref="QueueViewModel"/>, continued): done and cancelled items, read on demand, once.</summary>
internal sealed partial class QueueViewModel
{
    private bool _closedRead;

    /// <summary>The API the last refresh read from; the fold reads through it.</summary>
    internal Uri? LastUrl { get; private set; }

    /// <summary>True while the fold is open.</summary>
    public bool IsClosedOpen { get; private set { Set(ref field, value); OnChanged(nameof(ClosedArrow)); } }

    /// <summary>The fold's arrow: down while it is open, right while it is closed (one glyph, so the tree never holds both).</summary>
    public string ClosedArrow => IsClosedOpen ? "▾" : "▸";

    /// <summary>"Closed" before the first read, then "Closed 3" (the name; the view draws the label and the count apart).</summary>
    public string ClosedHeader { get; private set => Set(ref field, value); } = Labels.ClosedHeader(count: null);

    // The queue's size when the closed items were last read: a refresh that finds another size reads them again.
    private int _closedTotal = -1;

    /// <summary>The fold's label.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string ClosedLabel => "Closed";
#pragma warning restore CA1822, MA0041

    /// <summary>The closed count, muted beside the label; empty before the first read.</summary>
    public string ClosedCount { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The closed items, newest first; they carry no actions.</summary>
    public ObservableCollection<ItemRowViewModel> ClosedRows { get; } = [];

    /// <summary>Opens or closes the fold; the first opening reads done and cancelled.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the fold shows.</returns>
    internal async Task ToggleClosed(Uri url, CancellationToken cancellationToken)
    {
        IsClosedOpen = !IsClosedOpen;
        Columns();
        if (!IsClosedOpen || _closedRead)
        {
            return;
        }

        await ReadClosed(url, cancellationToken).ConfigureAwait(true);
    }

    // Reads done and cancelled; both failing leaves the fold as it was, so the next try reads again.
    private async Task ReadClosed(Uri url, CancellationToken cancellationToken)
    {
        var done = await api.ListItems(url, "done", cancellationToken).ConfigureAwait(true);
        var cancelled = await api.ListItems(url, "cancelled", cancellationToken).ConfigureAwait(true);
        if (done.IsError && cancelled.IsError)
        {
            return;   // nothing was read: the next opening tries again
        }

        _closedRead = true;
        _closedTotal = Total;
        var now = clock.GetUtcNow();
        var items = (done.IsSuccess ? done.Value.Items : []).Concat(cancelled.IsSuccess ? cancelled.Value.Items : []).OrderByDescending(i => i.Since);
        Patch.To(ClosedRows, [.. items.Select(i => new ItemRowViewModel(i, "closed", primaryAction: null, now))]);
        ClosedHeader = Labels.ClosedHeader(ClosedRows.Count);
        ClosedCount = ClosedRows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ShowsClosed = ClosedRows.Count > 0;
        Columns();
    }

    // After a refresh: learn whether there is anything closed, so the fold is hidden when there is not.
    private Task LearnClosed(Uri url, CancellationToken cancellationToken) =>
        _closedRead && _closedTotal == Total ? Task.CompletedTask : ReadClosed(url, cancellationToken);
}
