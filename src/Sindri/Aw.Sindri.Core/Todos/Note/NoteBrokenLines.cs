namespace Aw.Sindri.Core.Todos.Note;

/// <summary>The note's task lines that break the text rules (todo spec §4.5 D3): the lines that can't sync. Shared by the note read and doctor's 'todo' row.</summary>
public static class NoteBrokenLines
{
    /// <summary>The broken lines, in file order.</summary>
    /// <param name="document">The note as read.</param>
    /// <returns>One entry per task line whose break isn't None: its 1-based number and the reason in plain words ("it has a markdown link").</returns>
    public static IReadOnlyList<(int Line, string Reason)> Of(NoteDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return [.. document.Todos.Where(static t => t.Break != NoteBreak.None).OrderBy(static t => t.Number).Select(static t => (t.Number, NoteBreakReasons.Of(t.Break)))];
    }
}
