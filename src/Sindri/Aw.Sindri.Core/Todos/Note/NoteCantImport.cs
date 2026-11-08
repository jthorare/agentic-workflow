namespace Aw.Sindri.Core.Todos.Note;

/// <summary>An open task line of the note that an import can't adopt.</summary>
/// <param name="Line">The 1-based line number.</param>
/// <param name="Reason">Why, as the end of "line 14 can't sync: …" (<see cref="NoteBreakReasons.Of"/>).</param>
public sealed record NoteCantImport(int Line, string Reason);
