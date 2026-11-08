namespace Aw.Sindri.Core.Todos.Note;

/// <summary>An open hold: the changes the note made that wait for the owner (todo spec §4.5; the payload of 'todo.note.held').</summary>
/// <param name="Changes">The held changes.</param>
public sealed record NoteHold(IReadOnlyList<HeldChange> Changes)
{
    /// <summary>The re-read before a pick: the held changes that are still true in the note as it is now. A removal is true while no line carries the id; an edit or reopen is true while the first line with the id equals the held line. A pick acts on these only; a smaller set than the hold's is a stale row (SND-TODO-409).</summary>
    /// <param name="current">The note now.</param>
    /// <returns>The changes still true, in the hold's order.</returns>
    public IReadOnlyList<HeldChange> StillTrue(NoteDocument current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var first = new Dictionary<TodoId, NoteTodoLine>();
        foreach (var todo in current.Todos.Where(static t => t.Id is not null))
        {
            _ = first.TryAdd(todo.Id!.Value, todo); // why: filtered on the line before
        }

        return [.. Changes.Where(change => IsTrue(change, first))];
    }

    private static bool IsTrue(HeldChange change, Dictionary<TodoId, NoteTodoLine> first) =>
        first.TryGetValue(change.Id, out var line)
            ? change.Kind != HeldKind.Removed && string.Equals(line.Raw, change.NoteLine, StringComparison.Ordinal)
            : change.Kind == HeldKind.Removed;
}
