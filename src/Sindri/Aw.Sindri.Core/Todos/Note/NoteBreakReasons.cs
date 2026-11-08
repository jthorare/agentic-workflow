namespace Aw.Sindri.Core.Todos.Note;

/// <summary>The words the sync line and the doctor row use for a line that breaks D3 ("line 14 can't sync: it has a markdown link").</summary>
public static class NoteBreakReasons
{
    /// <summary>The reason, as the end of "line 14 can't sync: …".</summary>
    /// <param name="reason">The break; <see cref="NoteBreak.None"/> is refused.</param>
    /// <returns>For example "it has a markdown link".</returns>
    public static string Of(NoteBreak reason) => throw new NotSupportedException();
}
