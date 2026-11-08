namespace Aw.Sindri.Core.Todos.Note;

/// <summary>The three-way merge between the base, the note and Sindri, per todo id (todo spec §4.5). Pure.</summary>
public static class NoteMerge
{
    /// <summary>Merges. With a null base the pass adopts and appends, never removes, and a todo whose text differs on the two sides is a conflict.</summary>
    /// <param name="noteBase">The base, or null when there is none (or its hash didn't match).</param>
    /// <param name="note">The note now.</param>
    /// <param name="sindri">Sindri's todos now, removed and done ones included.</param>
    /// <param name="options">Minter, scrubber, picks, open conflicts and hold, the adoption cap.</param>
    /// <returns>The intents, the line edits, the base to save and the hold.</returns>
    public static NoteMergeResult Merge(NoteBase? noteBase, NoteDocument note, IReadOnlyList<SindriTodo> sindri, NoteMergeOptions options) => throw new NotSupportedException();
}
