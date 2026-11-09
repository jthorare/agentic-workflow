namespace Aw.Sindri.Ui.Api;

/// <summary>The answer of POST /sindri/todos/{id}/keep (todo spec §4.4): the todo as it reads after the pick, and when the Undo window closes. Both are optional: the app reads only what it needs.</summary>
/// <param name="Todo">The todo with the picked version.</param>
/// <param name="UndoUntil">When the pick's Undo window closes, on Sindri's clock.</param>
internal sealed record TodoKeepAcceptedView(TodoView? Todo = null, DateTimeOffset? UndoUntil = null);
