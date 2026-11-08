namespace Aw.Sindri.Core.Todos.Note;

/// <summary>The fields of a todo the merge compares, whichever side they came from. The done date is carried but never compared: ticking in the note without a date and in Sindri with today's is the same tick.</summary>
/// <param name="Text">The scrubbed text.</param>
/// <param name="Due">The due date.</param>
/// <param name="Links">The links the note can show.</param>
/// <param name="Done">True when done.</param>
/// <param name="DoneOn">The done date.</param>
internal sealed record NoteFields(string Text, DateOnly? Due, IReadOnlyList<string> Links, bool Done, DateOnly? DoneOn)
{
    public static NoteFields Of(SindriTodo todo) => new(todo.Text, todo.Due, NoteLinks.Renderable(todo.Links), todo.Done, todo.DoneOn);

    public bool SameAs(NoteFields other) => Done == other.Done && !FieldsDiffer(other);

    public bool FieldsDiffer(NoteFields other) =>
        !string.Equals(Text, other.Text, StringComparison.Ordinal) || Due != other.Due || !Links.SequenceEqual(other.Links, StringComparer.Ordinal);

    public NoteSide ToSide() => new(Text, Due, Links, Done, Removed: false);

    public bool Is(NoteSide side) =>
        !side.Removed && string.Equals(Text, side.Text, StringComparison.Ordinal) && Due == side.Due && Done == side.Done && Links.SequenceEqual(side.Links, StringComparer.Ordinal);

    /// <summary>What the note would have to tell Sindri to turn <paramref name="from"/> into this: the changed fields, then the tick or the untick.</summary>
    public IEnumerable<NoteIntent> IntentsFrom(TodoId id, NoteFields from)
    {
        if (FieldsDiffer(from))
        {
            yield return new NoteChange(
                id,
                string.Equals(Text, from.Text, StringComparison.Ordinal) ? null : Text,
                Due != from.Due,
                Due,
                Links.SequenceEqual(from.Links, StringComparer.Ordinal) ? null : Links);
        }

        if (Done && !from.Done)
        {
            yield return new NoteDone(id, DoneOn);
        }
        else if (!Done && from.Done)
        {
            yield return new NoteReopen(id);
        }
    }
}
