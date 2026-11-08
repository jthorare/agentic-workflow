namespace Aw.Sindri.Core.Todos.Note;

/// <summary>One line of the note.</summary>
/// <param name="Number">The 1-based line number.</param>
/// <param name="Text">The line without its line ending.</param>
/// <param name="Eol">Its line ending: "\n", "\r\n" or "" for a last line without one.</param>
/// <param name="Todo">The todo when this is a top-level task line, else null.</param>
public sealed record NoteLine(int Number, string Text, string Eol, NoteTodoLine? Todo);
