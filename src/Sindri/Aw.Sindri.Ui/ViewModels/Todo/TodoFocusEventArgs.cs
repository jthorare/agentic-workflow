namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>Where focus goes after a todo changed: a row, or the panel itself when no row is left.</summary>
/// <param name="id">The todo's id, or null for the panel.</param>
internal sealed class TodoFocusEventArgs(string? id) : EventArgs
{
    /// <summary>The todo's id, or null for the panel.</summary>
    internal string? Id { get; } = id;
}
