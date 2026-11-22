using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>The sheet's reads (<see cref="ItemSheetViewModel"/>, continued): the item and its timeline, newest read wins (L6).</summary>
internal sealed partial class ItemSheetViewModel
{
    private int _generation;   // bumped by every read and by a change of subject: an answer from an older one is dropped
    private int? _loadingMore;   // the subject (ItemSheetViewModel.Clear bumps it) a Load more is in flight for
    private CancellationTokenSource? _reads;
    private Uri? _timelineUrl;   // the connection the timeline shown was read from: on another one it is read again from the first page
    private bool _rereadTimeline;   // an answer said the views were rebuilding: the lines shown may be gone or reworded, so read them all again

    /// <summary>Opens an item: reads it and its timeline's first page.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="itemId">The item's id.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the sheet shows the answer.</returns>
    internal async Task Open(Uri url, string itemId, CancellationToken cancellationToken)
    {
        Show(itemId);
        _url = url;
        await ReadAndSettle(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Names the sheet's subject at once (a deep link, even before the app is connected): the skeleton shows until a read
    /// answers, and the first re-read after connecting loads it (§3.3).
    /// </summary>
    /// <param name="itemId">The item's id.</param>
    internal void Show(string itemId)
    {
        if (!string.Equals(itemId, ItemId, StringComparison.Ordinal))
        {
            Clear();
            ItemId = itemId;
        }

        IsLoading = !HasItem;
    }

    /// <summary>Re-reads the open item and the timeline lines newer than the ones shown (on every event, §9.2).</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the sheet shows the answer.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        if (ItemId.Length == 0)
        {
            return Task.CompletedTask;
        }

        _url = url;
        return ReadAndSettle(cancellationToken);
    }

    private async Task ReadAndSettle(CancellationToken cancellationToken)
    {
        if (await Read(cancellationToken).ConfigureAwait(true))
        {
            IsLoading = false;
            if (_url is { } url && HasItem)
            {
                await Needs.Read(url, ItemId, cancellationToken).ConfigureAwait(true);
                await Blocked.Read(url, ItemId, cancellationToken).ConfigureAwait(true);
            }
        }
    }

    /// <summary>Load more: appends the page the API's 'next' names. A second call while one is in flight does nothing.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the page is shown: true when it was appended.</returns>
    internal async Task<bool> LoadMore(CancellationToken cancellationToken)
    {
        if (_loadingMore == _subject || _url is not { } url || Next is not { } next)
        {
            return false;
        }

        (_loadingMore, var id) = (_subject, ItemId);
        var mine = _subject;
        try
        {
            var page = await api.Timeline(url, id, next, cancellationToken).ConfigureAwait(true);

            // The sheet moved to another item, or a re-read moved the cursor, while the page was on its way: it is not this page.
            if (!string.Equals(id, ItemId, StringComparison.Ordinal) || (page.IsSuccess && Next != next))
            {
                return false;
            }

            // A page that failed is said once, so the human who pressed the button hears why nothing came; the rows stay.
            return page.IsSuccess ? Append(page.Value) : PageFailed(page.Error);
        }
        finally
        {
            if (_loadingMore == mine)
            {
                _loadingMore = null;   // not another item's flag, set since
            }
        }
    }

    private bool Append(TimelineView page)
    {
        _rereadTimeline |= page.AsOf.Rebuilding;
        foreach (var row in Rows(page))
        {
            Timeline.Add(row);
        }

        Next = page.Next;
        return true;
    }

    private bool PageFailed(Error error)
    {
        Say($"{error.Code}: {error.Message}");
        return false;
    }

    // Reads the item and the pages shown so far; false when a newer read (or another item) took over and this answer was dropped.
    private async Task<bool> Read(CancellationToken cancellationToken)
    {
        if (_url is not { } url)
        {
            return true;
        }

        if (_reads is { } older)
        {
            await older.CancelAsync().ConfigureAwait(true);
        }

        var mine = _reads = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var (id, generation) = (ItemId, ++_generation);
        try
        {
            return await ReadItem(url, id, generation, mine.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;   // a newer read took over
        }
        finally
        {
            if (ReferenceEquals(_reads, mine))
            {
                _reads = null;

                // Abandoned by its caller with no newer read in flight: nothing will settle a conflict this read was holding.
                // Close goes on (its control speaks the refusal itself).
                if (cancellationToken.IsCancellationRequested)
                {
                    EndConflict();
                }
            }

            mine.Dispose();
        }
    }

    private async Task<bool> ReadItem(Uri url, string id, int generation, CancellationToken cancellationToken)
    {
        var item = await api.Item(url, id, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return false;
        }

        if (item.IsError)
        {
            Failed(item.Error);
            return true;
        }

        Show(item.Value);
        return await ReadTimeline(url, id, generation, cancellationToken).ConfigureAwait(true);
    }

    // A timeline line is written once, under its own ledger sequence, and the pages are oldest first (ItemTimelineProjection,
    // GetItemTimelineService): an event can only add lines after the newest one, so one read after it is the whole change. The
    // lines shown are read again from the first page only when they may differ: another URL, or the views rebuilt. A serve
    // restart on the same URL is not another URL: only its rebuilding answer, when the sheet reads one, rereads (#3876).
    private Task<bool> ReadTimeline(Uri url, string id, int generation, CancellationToken cancellationToken) =>
        _rereadTimeline || (Timeline.Count > 0 && !url.Equals(_timelineUrl))
            ? ReadShownTimeline(url, id, generation, cancellationToken)
            : ReadNewTimeline(url, id, generation, cancellationToken);

    // The lines after the newest one shown, appended; the rows shown are untouched, so their controls, focus and scroll stay.
    private async Task<bool> ReadNewTimeline(Uri url, string id, int generation, CancellationToken cancellationToken)
    {
        if (Next is not null)
        {
            return true;   // newer lines sit past the pages Load more hasn't read yet: nothing shown can change
        }

        var newest = Timeline.Count > 0 ? Timeline[^1].Sequence : 0;
        var answer = await api.Timeline(url, id, newest, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return false;
        }

        if (answer.IsError)
        {
            return true;
        }

        foreach (var row in Rows(answer.Value).Where(r => r.Sequence > newest))
        {
            Timeline.Add(row);
        }

        (Next, _timelineUrl) = (answer.Value.Next, url);
        _rereadTimeline |= answer.Value.AsOf.Rebuilding;
        return true;
    }

    // Every page from the first up to the newest line shown (a re-read never shrinks what Load more showed). The end is read
    // again after every page: a Load more that answers while this read waits moves it, and its page must stay (#3876).
    private async Task<bool> ReadShownTimeline(Uri url, string id, int generation, CancellationToken cancellationToken)
    {
        var (rows, rebuilding) = (new List<TimelineRow>(), false);
        long? after = 0;
        do
        {
            var answer = await api.Timeline(url, id, after.Value, cancellationToken).ConfigureAwait(true);
            if (generation != _generation)
            {
                return false;
            }

            if (answer.IsError)
            {
                return true;   // the old timeline stays, in full, and the next read tries again from the first page
            }

            rows.AddRange(Rows(answer.Value));
            (after, rebuilding) = (answer.Value.Next, rebuilding || answer.Value.AsOf.Rebuilding);
        }
        while (after is { } cursor && cursor < ShownEnd());

        Keep(rows);
        (Next, _timelineUrl, _rereadTimeline) = (after, url, rebuilding);
        return true;
    }

    // The cursor the lines shown end at: Load more's next page, or the newest line when every page is shown.
    private long ShownEnd() => Next ?? (Timeline.Count > 0 ? Timeline[^1].Sequence : 0);

    // The row already shown for an entry (same sequence, same fields) stays, so its control and the focus on it do.
    private void Keep(List<TimelineRow> rows)
    {
        var shown = new Dictionary<long, TimelineRow>();
        foreach (var row in Timeline)
        {
            _ = shown.TryAdd(row.Sequence, row);
        }

        Patch.To(Timeline, [.. rows.Select(r => shown.TryGetValue(r.Sequence, out var kept) && kept == r ? kept : r)]);
    }

    private IEnumerable<TimelineRow> Rows(TimelineView page) => page.Entries.Select(e => new TimelineRow(
        e.Sequence, TimeZoneInfo.ConvertTime(e.Time, clock.LocalTimeZone).ToString("HH:mm:ss", CultureInfo.InvariantCulture), e.Type, e.Actor, e.Line));
}
