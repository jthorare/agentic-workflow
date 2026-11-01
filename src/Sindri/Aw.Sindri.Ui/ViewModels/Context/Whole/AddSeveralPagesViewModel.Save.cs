using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The sheet's write and Esc (<see cref="AddSeveralPagesViewModel"/>, continued).</summary>
internal sealed partial class AddSeveralPagesViewModel
{
    /// <summary>Writes every ticked row's page in one profile write, sent with the hash the list was read at.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>When the answer is drawn.</returns>
    internal async Task SaveAll(CancellationToken cancellationToken)
    {
        if (!CanSave)
        {
            return;
        }

        var save = Pending();
        var approve = Approves(save);
        Say(string.Empty);
        IsSaving = true;
        OnChanged(nameof(IsSaving));
        ChangedSave();
        PageSaved saved;
        try
        {
            // The rows are built on the preview's profileHash, so that is this sheet's ifHash; #1826's fileHash wiring is for the single page sheet only.
            saved = await _writes.Save(save, _hash, approve, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;   // the sheet closed while the job ran: the write was sent and may still land, so nothing reads as failed
        }
        finally
        {
            IsSaving = false;
            OnChanged(nameof(IsSaving));
            ChangedSave();
        }

        await Answer(saved, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Esc, or the close affordance: the discard question is answered with its safe choice; changes ask first; otherwise the sheet closes.</summary>
    /// <returns>True when the key was spent on the sheet.</returns>
    internal bool Escape()
    {
        if (IsSaving)
        {
            return true;
        }

        if (IsConfirmingDiscard)
        {
            KeepEditing();
            return true;
        }

        if (HasChanges)
        {
            IsConfirmingDiscard = true;
            OnChanged(nameof(IsConfirmingDiscard));
            FocusRequested?.Invoke(this, new ValueEventArgs<AddSeveralPagesFocus>(AddSeveralPagesFocus.KeepEditing));
            return true;
        }

        Close();
        return true;
    }

    /// <summary>Goes back to the sheet from the discard question.</summary>
    internal void KeepEditing()
    {
        if (!IsConfirmingDiscard)
        {
            return;
        }

        IsConfirmingDiscard = false;
        OnChanged(nameof(IsConfirmingDiscard));
        FocusRequested?.Invoke(this, new ValueEventArgs<AddSeveralPagesFocus>(AddSeveralPagesFocus.Text));
    }

    // The write for the ticked rows: their pages, and the key paths that already waited, so a page with no id yet finds its own path afterwards.
    private PageSave Pending() =>
        new([.. Rows.Where(static r => r.Ticked).Select(static r => r.Page).OfType<PagesPreviewPage>().Select(Draft)], []) { Before = _waiting?.ChangedPaths ?? [] };

    // One click saves and approves only when nothing but these pages waits.
    private bool Approves(PageSave save) => _waiting?.OnlyMine(PageWrites.Mine(save)) == true;

    private static PageDraft Draft(PagesPreviewPage page) =>
        new(page.Id, page.Name, page.Repo, page.Surface, Owner: false, [.. page.Actions.Select(static a => new PageActionDraft(a.Id, a.Name, a.Action, a.Concepts))]);

    // The profile moved: read the same list again so the rows show what is there now, keep the ticks of the rows that can still be ticked, and take the new hash.
    private async Task Clashed(CancellationToken cancellationToken)
    {
        var ticked = Rows.Where(static r => r.Ticked).Select(static r => r.Block).ToHashSet();
        var picks = Rows.ToDictionary(static r => r.Block, static r => r.Picks);
        var read = await _writes.Preview(_readText, _repo, cancellationToken).ConfigureAwait(true);
        _waiting = await _writes.Waiting(cancellationToken).ConfigureAwait(true);
        if (read.Preview is { } preview)
        {
            Fill(preview, _readText);
            foreach (var row in Rows)
            {
                if (picks.TryGetValue(row.Block, out var picked))
                {
                    row.Restore(picked);   // the pickers come back with their picks, so a ticked row stays tickable
                }

                row.Ticked = ticked.Contains(row.Block);   // a row that can't be ticked any more stays off
            }

            Say(Labels.AddSeveralPagesClash);
        }
        else
        {
            Fill(new PagesPreview(string.Empty, string.Empty, []), string.Empty);   // no fresh read, no stale rows to save
            SayText(Refusal(read));
        }

        ChangedSave();
    }

    private async Task Answer(PageSaved saved, CancellationToken cancellationToken)
    {
        switch (saved.State)
        {
            case PageSavedState.Approved or PageSavedState.Waiting:
                Saved?.Invoke(this, EventArgs.Empty);
                Close();
                break;
            case PageSavedState.Clash:
                await Clashed(cancellationToken).ConfigureAwait(true);
                break;
            case PageSavedState.Refused:
            case PageSavedState.Failed:
            default:
                Say(saved.Message is { Length: > 0 } message ? message : Labels.AddSeveralPagesFailed);
                break;
        }
    }
}
