using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The Accept all suggested mode (<see cref="AddSeveralPagesViewModel"/>, continued).</summary>
internal sealed partial class AddSeveralPagesViewModel
{
    private const int PageCap = 200;

    /// <summary>
    /// Reads the section's suggested pages, how many pages the profile holds and what waits, and draws one ticked row per page: the first 200 in the section's order, those past the profile's
    /// room unticked with their reason. Run again after a clash, each row (matched by its surface key) keeps its tick, name and picks.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the rows are drawn.</returns>
    internal async Task LoadSuggested(CancellationToken cancellationToken)
    {
        if (_acceptKeys is not { } keys)
        {
            return;
        }

        var before = Rows.ToDictionary(static r => r.Key, StringComparer.Ordinal);
        Say(string.Empty);
        IsReading = true;
        OnChanged(nameof(IsReading));
        IReadOnlyList<PageDraft>? drafts;
        int? owners;
        try
        {
            drafts = await _writes.Suggested(_acceptConcept, keys, cancellationToken).ConfigureAwait(true);
            owners = await _writes.OwnerCount(cancellationToken).ConfigureAwait(true);
            _waiting = await _writes.Waiting(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;   // the sheet closed while the pages were read
        }
        finally
        {
            IsReading = false;
            OnChanged(nameof(IsReading));
        }

        _hash = _waiting?.FileHash ?? _waiting?.LiveHash;   // never a null hash written: no hash, no Save
        DrawSuggested(drafts, owners, before);
        ChangedSave();
        FocusRequested?.Invoke(this, new ValueEventArgs<AddSeveralPagesFocus>(AddSeveralPagesFocus.FirstRow));
    }

    private void DrawSuggested(IReadOnlyList<PageDraft>? drafts, int? owners, Dictionary<string, AddSeveralPagesRowViewModel> before)
    {
        if (drafts is null)
        {
            MoreText = string.Empty;
            SetRows([]);
            Say(Labels.AddSeveralPagesUnreadable);
            return;
        }

        _ = _pickConcepts;
        var room = Math.Max(0, PageCap - (owners ?? 0));   // an unreadable count is left to the server's refusal
        SetRows([.. drafts.Take(PageCap).Select((d, i) => AddSeveralPagesRowViewModel.FromDraft(d, i + 1, i >= room ? Labels.AcceptAllProfileFull : null, _known))]);
        foreach (var row in Rows.Where(r => before.ContainsKey(r.Key)))
        {
            var kept = before[row.Key];
            row.Name = kept.Name;
            row.Restore(kept.Picks);
            row.RestoreTicks(kept.ActionTicks);
            row.Ticked = kept.Ticked;
        }

        MoreText = drafts.Count > PageCap ? Labels.AcceptAllMore(drafts.Count - PageCap) : string.Empty;
        OnChanged(nameof(MoreText));
        OnChanged(nameof(HasMore));
        if (_hash is null)
        {
            Say(Labels.PageSheetNoHash);
        }
    }
}
