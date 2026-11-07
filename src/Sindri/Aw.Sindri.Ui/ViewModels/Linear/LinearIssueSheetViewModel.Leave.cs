using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>The question before unsaved edits go: Esc, Back or another issue (<see cref="LinearIssueSheetViewModel"/>, continued).</summary>
internal sealed partial class LinearIssueSheetViewModel
{
    /// <summary>Esc or Back: the discard question answers its safe choice; unsaved edits ask first.</summary>
    /// <returns>True when the key was spent on the question; false when nothing is unsaved and the sheet may close.</returns>
    internal bool Escape()
    {
        if (IsConfirmingDiscard)
        {
            KeepEditing();
            return true;
        }

        return Leaving(next: null);
    }

    /// <summary>The sheet is about to close or show another issue: asks when edits are unsaved.</summary>
    /// <param name="next">The issue to open afterwards; null to close the sheet.</param>
    /// <returns>True when the question is showing and the sheet must wait for the answer.</returns>
    internal bool Leaving(string? next)
    {
        if (!HasChanges)
        {
            return false;
        }

        _leaveTo = next;
        IsConfirmingDiscard = true;
        OnChanged(nameof(DiscardBody));
        FocusRequested?.Invoke(this, new ValueEventArgs<LinearIssueFocus>(LinearIssueFocus.KeepEditing));
        return true;
    }

    /// <summary>Goes back to the form from the discard question.</summary>
    internal void KeepEditing()
    {
        if (!IsConfirmingDiscard)
        {
            return;
        }

        IsConfirmingDiscard = false;
        FocusRequested?.Invoke(this, new ValueEventArgs<LinearIssueFocus>(LinearIssueFocus.Title));
    }

    /// <summary>Drops the edits and lets the leaving go ahead.</summary>
    internal void Discard()
    {
        if (!IsConfirmingDiscard)
        {
            return;
        }

        foreach (var field in Editable)
        {
            field.Revert();
        }

        (ConflictRows, HasConflict, IsConfirmingDiscard) = ([], false, false);
        var next = _leaveTo;
        _leaveTo = null;
        LeaveConfirmed?.Invoke(this, new ValueEventArgs<string?>(next));
    }
}
