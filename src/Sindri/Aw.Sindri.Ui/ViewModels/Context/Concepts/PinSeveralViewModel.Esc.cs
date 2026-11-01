namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The Add several sheet's Esc (concept pins spec §3.6 "Keyboard"): ask before a pasted list or an edit is thrown away.</summary>
#pragma warning disable MA0182 // Q7b: the shell sheet mounts it
internal sealed partial class PinSeveralViewModel
{
    /// <summary>Asking to discard puts focus on Keep editing: the view moves it.</summary>
    internal event EventHandler? KeepEditingFocusRequested;

    /// <summary>True while "Discard your changes?" is asked.</summary>
    public bool IsConfirmingDiscard { get; private set => Set(ref field, value); }

    /// <summary>The question before a discard.</summary>
    public string DiscardQuestion { get; } = Labels.PinSheetDiscardQuestion;

    /// <summary>Discards.</summary>
    public string DiscardText { get; } = Labels.PinSheetDiscard;

    /// <summary>Goes back to the list.</summary>
    public string KeepEditingText { get; } = Labels.PinSheetKeepEditing;

    /// <summary>Gets a value indicating whether closing would lose something: the box has text or a row was edited.</summary>
    internal bool IsDirty => Text.Trim().Length > 0 || Rows.Any(r => r.Edited);

    /// <summary>Esc: a second Esc keeps editing, else asks before throwing changes away, else closes without writing.</summary>
    /// <returns>True: Esc was spent here.</returns>
    internal bool Escape()
    {
        if (_writing)
        {
            return true;
        }

        if (IsConfirmingDiscard)
        {
            KeepEditing();
        }
        else if (IsDirty)
        {
            IsConfirmingDiscard = true;
            KeepEditingFocusRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _close();
        }

        return true;
    }

    /// <summary>Discards the list and closes; nothing is written.</summary>
    internal void Discard() => _close();

    /// <summary>Goes back to the list from the discard question.</summary>
    internal void KeepEditing() => IsConfirmingDiscard = false;
}
#pragma warning restore MA0182
