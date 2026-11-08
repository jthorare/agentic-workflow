namespace Aw.Sindri.Core.Todos.Note;

/// <summary>Counts what a note holds and how much of it an import would take (todo spec §3.4, §4.4 'note/check'). Pure.</summary>
public static class NoteCounter
{
    /// <summary>The most open todos Sindri holds (SND-TODO-006).</summary>
    public const int MaxOpen = 500;

    /// <summary>Counts the lines of a parsed note.</summary>
    /// <param name="note">The note.</param>
    /// <returns>The counts.</returns>
    public static NoteCounts Count(NoteDocument note)
    {
        ArgumentNullException.ThrowIfNull(note);
        var todos = note.Todos.ToList();
        var open = todos.Where(static t => !t.Done).ToList();
        var broken = open.Where(static t => t.Break != NoteBreak.None).ToList();
        return new NoteCounts(open.Count, todos.Count - open.Count, note.Lines.Count - todos.Count, open.Count - broken.Count, [.. broken.Select(static t => new NoteCantImport(t.Number, NoteBreakReasons.Of(t.Break)))]);
    }

    /// <summary>How many of the importable lines fit under the open cap beside the todos Sindri already holds.</summary>
    /// <param name="importable">Open task lines that can import.</param>
    /// <param name="sindriOpen">Open todos Sindri already has.</param>
    /// <returns>The number an import would adopt: never negative, at most <paramref name="importable"/>.</returns>
    public static int WillImport(int importable, int sindriOpen) => Math.Clamp(MaxOpen - sindriOpen, 0, importable);
}
