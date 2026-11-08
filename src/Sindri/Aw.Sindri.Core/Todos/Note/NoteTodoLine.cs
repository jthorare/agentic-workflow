
namespace Aw.Sindri.Core.Todos.Note;

/// <summary>A top-level task line of the note, read (todo spec §4.5). When <paramref name="Break"/> isn't None the other fields are what the parser peeled off the line's end; the rest stays in <paramref name="Text"/>.</summary>
/// <param name="Number">The 1-based line number.</param>
/// <param name="Raw">The line without its line ending, byte for byte.</param>
/// <param name="Id">The Sindri id ('td_' plus the 26 lowercase ULID characters of the trailing '^td-…' block id), or null for a line with no valid id.</param>
/// <param name="RejectedBlock">A trailing '^td-…' that is not 26 lowercase ULID characters, as written (caret included); it is not part of <paramref name="Text"/>. Null when there is none.</param>
/// <param name="Text">The text, with the escapes of <see cref="NoteRenderer.Escape"/> undone.</param>
/// <param name="Due">The trailing due date, or null.</param>
/// <param name="Done">True for '[x]' or '[X]'.</param>
/// <param name="DoneOn">The date of the trailing done marker, or null.</param>
/// <param name="Links">The trailing 'sindri://' deep links, in line order.</param>
/// <param name="Break">Why the line breaks D3, or None.</param>
public sealed record NoteTodoLine(
    int Number,
    string Raw,
    TodoId? Id,
    string? RejectedBlock,
    string Text,
    DateOnly? Due,
    bool Done,
    DateOnly? DoneOn,
    IReadOnlyList<string> Links,
    NoteBreak Break);
