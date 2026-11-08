namespace Aw.Sindri.Ui.Api;

/// <summary>The answer of POST /sindri/todos/{id}/keep/undo (todo spec §4.4): the todo with its conflict open again.</summary>
/// <param name="Todo">The todo with the conflict.</param>
internal sealed record TodoKeepUndoneView(TodoView? Todo = null);
