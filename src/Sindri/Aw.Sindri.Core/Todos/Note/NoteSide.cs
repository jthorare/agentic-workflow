namespace Aw.Sindri.Core.Todos.Note;

/// <summary>One side of a conflict: a todo as that side has it (todo spec §4.1 'todo.conflicted').</summary>
/// <param name="Text">The scrubbed text; null when <paramref name="Removed"/>.</param>
/// <param name="Due">The due date, or null.</param>
/// <param name="Links">The links.</param>
/// <param name="Done">True when done.</param>
/// <param name="Removed">True when that side removed the todo.</param>
public sealed record NoteSide(string? Text, DateOnly? Due, IReadOnlyList<string> Links, bool Done, bool Removed);
