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

    /// <summary>Opens an item: reads it and its timeline's first page.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="itemId">The item's id.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the sheet shows the answer.</returns>
    internal async Task Open(Uri url, string itemId, CancellationToken cancellationToken)
    {
        Show(itemId);
        (_url, _pages) = (url, Math.Max(_pages, 1));
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

    /// <summary>Re-reads the open item and the timeline pages read so far (on every event, §9.2).</summary>
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
        _pages = Math.Max(_pages, 1);
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
        _pages++;
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

    // The pages read so far, from the first (a refresh never shrinks what Load more showed).
    private async Task<bool> ReadTimeline(Uri url, string id, int generation, CancellationToken cancellationToken)
    {
        var rows = new List<TimelineRow>();
        long? after = 0;
        for (var page = 0; page < _pages && after is { } cursor; page++)
        {
            var answer = await api.Timeline(url, id, cursor, cancellationToken).ConfigureAwait(true);
            if (generation != _generation)
            {
                return false;
            }

            if (answer.IsError)
            {
                return true;
            }

            rows.AddRange(Rows(answer.Value));
            after = answer.Value.Next;
        }

        // The row already shown for an entry (same sequence, same fields) stays, so its control and the focus on it do.
        var shown = new Dictionary<long, TimelineRow>();
        foreach (var row in Timeline)
        {
            _ = shown.TryAdd(row.Sequence, row);
        }

        Patch.To(Timeline, [.. rows.Select(r => shown.TryGetValue(r.Sequence, out var kept) && kept == r ? kept : r)]);
        Next = after;
        return true;
    }

    private IEnumerable<TimelineRow> Rows(TimelineView page) => page.Entries.Select(e => new TimelineRow(
        e.Sequence, TimeZoneInfo.ConvertTime(e.Time, clock.LocalTimeZone).ToString("HH:mm:ss", CultureInfo.InvariantCulture), e.Type, e.Actor, e.Line));
}
