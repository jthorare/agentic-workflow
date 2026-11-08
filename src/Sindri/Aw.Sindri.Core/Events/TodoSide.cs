namespace Aw.Sindri.Core.Events;

/// <summary>One side of a 'todo.conflicted' payload (todo spec §4.1): the todo as Sindri or the note has it; the text is scrubbed.</summary>
/// <param name="Text">The todo's text; null when <paramref name="Removed"/>.</param>
/// <param name="Due">Its due date, or null.</param>
/// <param name="Links">Its links, as deep links.</param>
/// <param name="Done">Whether it is checked off.</param>
/// <param name="Removed">Whether this side removed the todo (the other fields are then empty).</param>
public sealed record TodoSide(string? Text, DateOnly? Due, IReadOnlyList<string> Links, bool Done, bool Removed);
