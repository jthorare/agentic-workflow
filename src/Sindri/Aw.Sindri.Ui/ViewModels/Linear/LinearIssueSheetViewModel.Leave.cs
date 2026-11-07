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
    /// <param name="then">What to do once Discard is pressed (the shell's own way on, such as following a link or Back); when set, <see cref="LeaveConfirmed"/> is not raised.</param>
    /// <returns>True when the question is showing and the sheet must wait for the answer.</returns>
    internal bool Leaving(string? next, Action? then = null)
    {
        if (!HasChanges)
        {
            return false;
        }

        (_leaveTo, _then) = (next, then);
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

        (IsConfirmingDiscard, _then) = (false, null);
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
        var (next, then) = (_leaveTo, _then);
        (_leaveTo, _then) = (null, null);
        if (then is not null)
        {
            then();
        }
        else
        {
            LeaveConfirmed?.Invoke(this, new ValueEventArgs<string?>(next));
        }
    }
}
