
namespace Aw.Sindri.Core.Todos.Note;

/// <summary>A line with an id Sindri doesn't know: a new todo with that id (also how a crash between the swap and the event heals).</summary>
/// <param name="Id">The id from the line.</param>
/// <param name="Text">The scrubbed text.</param>
/// <param name="Due">The due date, or null.</param>
/// <param name="Links">The links.</param>
/// <param name="Done">True for an '[x]' line.</param>
/// <param name="DoneOn">The done date, or null.</param>
public sealed record NoteAdd(TodoId Id, string Text, DateOnly? Due, IReadOnlyList<string> Links, bool Done, DateOnly? DoneOn) : NoteIntent(Id);
