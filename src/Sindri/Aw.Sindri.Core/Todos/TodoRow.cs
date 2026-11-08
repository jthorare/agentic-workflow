using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Todos;

/// <summary>One todo in the 'todo' view (todo spec §4.2), keyed by its id.</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Text">The text, scrubbed.</param>
/// <param name="Due">The due date, or null.</param>
/// <param name="Links">The links, as deep links.</param>
/// <param name="AddedAt">When it was added.</param>
/// <param name="AddedBy">Who added it (the 'by' of 'todo.added').</param>
/// <param name="AddedVia">The 'via' of 'todo.added', or null.</param>
/// <param name="DoneAt">When it was checked off, or null while open.</param>
/// <param name="Removed">Whether it is removed.</param>
/// <param name="RemovedAt">When it was removed, or null.</param>
/// <param name="RemovedBy">Who removed it, or null.</param>
/// <param name="LastLineHash">A removed todo's last note line (tells a stale copy from an owner re-add), or null.</param>
/// <param name="Version">The version the row is at.</param>
/// <param name="LastBy">Who wrote last.</param>
/// <param name="LastVia">The 'via' of the last write, or null.</param>
/// <param name="Conflict">The open both-places conflict, or null.</param>
/// <param name="PickedConflict">The conflict a pick closed, kept so its Undo can open it again, or null.</param>
/// <param name="Sequence">The ledger sequence of the last event folded in.</param>
public sealed record TodoRow(
    TodoId Id,
    string Text,
    DateOnly? Due,
    IReadOnlyList<string> Links,
    DateTimeOffset AddedAt,
    string AddedBy,
    string? AddedVia,
    DateTimeOffset? DoneAt,
    bool Removed,
    DateTimeOffset? RemovedAt,
    string? RemovedBy,
    string? LastLineHash,
    long Version,
    string LastBy,
    string? LastVia,
    TodoOpenConflict? Conflict,
    TodoOpenConflict? PickedConflict,
    long Sequence);
