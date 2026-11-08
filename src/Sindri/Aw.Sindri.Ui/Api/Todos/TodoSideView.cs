namespace Aw.Sindri.Ui.Api.Todos;

/// <summary>One side of a both-places conflict (todo spec §4.1 todo.conflicted): the todo as Sindri or the note has it.</summary>
/// <param name="Text">The text; null on a side that removed the todo.</param>
/// <param name="Due">The due day, or null.</param>
/// <param name="Done">True when that side has it ticked.</param>
/// <param name="Removed">True when that side removed it.</param>
internal sealed record TodoSideView(string? Text, DateOnly? Due, bool Done, bool Removed = false);
