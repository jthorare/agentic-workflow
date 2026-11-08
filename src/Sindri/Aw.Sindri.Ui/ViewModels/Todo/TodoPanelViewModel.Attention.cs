namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// The panel's attention rows (<see cref="TodoPanelViewModel"/>, continued; todo spec §3.1, §3.3, §3.4, D11, D13): the open conflicts, oldest first, then the hold. One is shown, folded to a line
/// or open to its full form; the rest fold into "Sort out N more". They count inside the five rows, at most two slots. 1 keeps Sindri's side (a hold: puts the lines back in the note), 2 keeps
/// Obsidian's (a hold: applies in Sindri); a pick shows a toast with a 10 s countdown whose Undo reopens the row.
/// </summary>
internal sealed partial class TodoPanelViewModel
{
    /// <summary>The attention row that is shown, or null when nothing needs sorting out.</summary>
    public AttentionRowViewModel? Attention { get; private set => Set(ref field, value); }

    /// <summary>True while an attention row is shown.</summary>
    public bool HasAttention => Attention is not null;

    /// <summary>True while the shown row is open to its full form.</summary>
    public bool IsAttentionOpen => Attention is { IsOpen: true };

    /// <summary>"Sort out 2 more" when more attention rows wait behind the shown one; empty otherwise.</summary>
    public string MoreText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"2 more to sort out" for a screen reader; empty when none wait.</summary>
    public string MoreName { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while attention rows wait behind the shown one.</summary>
    public bool HasMore => MoreText.Length > 0;

    /// <summary>Raised after a pick Sindri accepted: the toast with its countdown and its Undo.</summary>
    internal event EventHandler<TodoPickedEventArgs>? Picked;

    /// <summary>Raised when focus should move onto the open attention row's first button.</summary>
    internal event EventHandler? AttentionButtonsRequested;

    /// <summary>Enter on the attention row: opens it to its full form and moves focus onto its first button.</summary>
    /// <returns>True when a row was shown to open.</returns>
    internal bool OpenAttention()
    {
        OnAttentionButtonsRequested();
        throw new NotSupportedException();
    }

    /// <summary>Esc on the open attention row: folds it back to its line.</summary>
    /// <returns>True when a row was open.</returns>
    internal bool CloseAttention() => throw new NotSupportedException();

    /// <summary>Enter on the "Sort out N more" line: shows the next attention row; the one shown folds.</summary>
    /// <returns>True when another row waited.</returns>
    internal bool OpenNextAttention() => throw new NotSupportedException();

    /// <summary>X, Space or Delete on a conflict row: nothing is sent, the row says "Pick a version first".</summary>
    /// <returns>True when a conflict row was shown (the key is spent).</returns>
    internal bool NoPick() => throw new NotSupportedException();

    /// <summary>1 or 2 on the attention row, or its button: 1 is Sindri's side (a hold: Put them back), 2 is Obsidian's (a hold: Apply).</summary>
    /// <param name="digit">1 or 2.</param>
    /// <returns>A task that completes when Sindri has answered.</returns>
    internal Task Pick(int digit)
    {
        OnPicked(new TodoPickedEventArgs(digit.ToString(System.Globalization.CultureInfo.InvariantCulture), () => Task.FromResult<string?>(null)));
        throw new NotSupportedException();
    }

    private void OnPicked(TodoPickedEventArgs args) => Picked?.Invoke(this, args);

    private void OnAttentionButtonsRequested() => AttentionButtonsRequested?.Invoke(this, EventArgs.Empty);
}
