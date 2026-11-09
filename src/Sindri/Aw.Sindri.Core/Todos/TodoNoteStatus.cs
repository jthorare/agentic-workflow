namespace Aw.Sindri.Core.Todos;

/// <summary>The note sync's in-memory status (todo spec §4.5 Failures: reset by a restart; the last sync comes from the ledger).</summary>
/// <param name="State">Where it stands.</param>
/// <param name="LastCheckAt">When the last pass ran; null before the first.</param>
/// <param name="LastError">Why the last pass failed; null when it didn't.</param>
/// <param name="FailedInARow">Failed passes in a row (the doctor row warns at 3).</param>
/// <param name="NotAdopted">Bare lines left as text in one-way mode or past the adoption limit.</param>
public sealed record TodoNoteStatus(TodoNoteState State, DateTimeOffset? LastCheckAt, string? LastError, int FailedInARow, int NotAdopted)
{
    /// <summary>Before any pass.</summary>
    public static TodoNoteStatus NotSet { get; } = new(TodoNoteState.NotSet, LastCheckAt: null, LastError: null, FailedInARow: 0, NotAdopted: 0);
}
