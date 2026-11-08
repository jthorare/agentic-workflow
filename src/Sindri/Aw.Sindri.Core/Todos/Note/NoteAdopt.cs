
namespace Aw.Sindri.Core.Todos.Note;

/// <summary>A new line with no valid id: a new todo with a minted id, which the pass also writes onto the line (a Replace edit that keeps the line's raw text).</summary>
/// <param name="Id">The minted id.</param>
/// <param name="Text">The scrubbed text.</param>
/// <param name="Due">The due date, or null.</param>
/// <param name="Links">The links.</param>
/// <param name="Done">True for an '[x]' line.</param>
/// <param name="DoneOn">The done date, or null.</param>
/// <param name="LineNumber">The line it was adopted from.</param>
public sealed record NoteAdopt(TodoId Id, string Text, DateOnly? Due, IReadOnlyList<string> Links, bool Done, DateOnly? DoneOn, int LineNumber) : NoteIntent(Id);
