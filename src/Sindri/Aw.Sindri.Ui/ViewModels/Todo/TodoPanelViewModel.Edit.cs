namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>The panel's row edit (<see cref="TodoPanelViewModel"/>, continued; todo spec §3.3, §3.4, D9): Enter edits in place, Enter saves, Esc cancels.</summary>
internal sealed partial class TodoPanelViewModel
{
    /// <summary>The edit that is open, or null.</summary>
    internal TodoEditViewModel? Editing { get; private set; }

    /// <summary>Enter on a row: opens the edit.</summary>
    /// <param name="id">The todo's id.</param>
    /// <returns>True when the edit opened.</returns>
    internal bool BeginEdit(string id) => throw new NotSupportedException();

    /// <summary>Enter in a field: saves what changed.</summary>
    /// <returns>A task that completes when Sindri has answered.</returns>
    internal Task SaveEdit() => throw new NotSupportedException();

    /// <summary>Esc in a field: closes the edit and sends nothing.</summary>
    /// <returns>True when an edit was open.</returns>
    internal bool CancelEdit() => throw new NotSupportedException();
}
