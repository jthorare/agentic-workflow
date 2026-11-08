namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>The panel's row keys (<see cref="TodoPanelViewModel"/>, continued; todo spec §3.3, §3.4): X or Space checks off or reopens, Delete removes.</summary>
internal sealed partial class TodoPanelViewModel
{
    /// <summary>True while focus is inside the panel (the Todo region, spec D6); the view sets it.</summary>
    public bool HasFocus { get; internal set => Set(ref field, value); }

    /// <summary>Raised after a key changed a todo and Sindri accepted it: the toast with its Undo.</summary>
    internal event EventHandler<TodoActedEventArgs>? Acted;

    /// <summary>Raised when focus should move to a row (or to the panel when the id is null).</summary>
    internal event EventHandler<TodoFocusEventArgs>? FocusRequested;

    /// <summary>X or Space on a row: checks an open todo off, reopens a done one, restores a removed one.</summary>
    /// <param name="id">The todo's id.</param>
    /// <returns>A task that completes when Sindri has answered.</returns>
    internal Task Toggle(string id)
    {
        OnFocusRequested(id);   // the red stub: the events exist, nothing acts yet
        throw new NotSupportedException();
    }

    /// <summary>Delete or Backspace on a row: removes the todo (it stays restorable).</summary>
    /// <param name="id">The todo's id.</param>
    /// <returns>A task that completes when Sindri has answered.</returns>
    internal Task Remove(string id)
    {
        OnActed(new TodoActedEventArgs(id, () => Task.FromResult<string?>(null)));
        throw new NotSupportedException();
    }

    private void OnActed(TodoActedEventArgs e) => Acted?.Invoke(this, e);

    private void OnFocusRequested(string? id) => FocusRequested?.Invoke(this, new TodoFocusEventArgs(id));
}
