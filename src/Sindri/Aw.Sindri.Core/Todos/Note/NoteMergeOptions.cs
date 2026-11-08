using System.Collections.Frozen;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Todos.Note;

/// <summary>What a pass brings to the merge besides the three sides.</summary>
/// <param name="Minter">Mints the ids of adopted lines, one pass-wide instance reused across re-writes.</param>
/// <param name="Scrubber">Scrubs the text read from the note and the base before comparing (REQ-W2).</param>
public sealed record NoteMergeOptions(PassIdMinter Minter, ITextScrubber Scrubber)
{
    /// <summary>For an id with a conflict just picked: the note line the pick was resolved against, which is that id's base line (D11).</summary>
    public IReadOnlyDictionary<TodoId, string> PickBases { get; init; } = FrozenDictionary<TodoId, string>.Empty;

    /// <summary>Ids named by a pick whose Undo window is open: their lines get no edit and their base lines are kept (D11).</summary>
    public IReadOnlySet<TodoId> LeaveAlone { get; init; } = FrozenSet<TodoId>.Empty;

    /// <summary>Open conflicts by id, each with the note side it was raised against.</summary>
    public IReadOnlyDictionary<TodoId, NoteSide> OpenConflicts { get; init; } = FrozenDictionary<TodoId, NoteSide>.Empty;

    /// <summary>The open hold, or null.</summary>
    public NoteHold? OpenHold { get; init; }

    /// <summary>At most this many new lines are adopted in one pass; the rest wait for the next.</summary>
    public int MaxAdoptions { get; init; } = 200;
}
