using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetTodoNote;

/// <summary>The note's state (todo spec §4.4 'sindri todo note'). The open hold is not served yet.</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="State">'not set', 'waiting for import', 'syncing', 'one way', 'can't read' or 'note busy'.</param>
/// <param name="Vault">The vault of the note (todo.obsidian); null when not set.</param>
/// <param name="Note">The note's path in the vault; null when not set.</param>
/// <param name="LastCheckAt">The last pass since the control plane started, or null.</param>
/// <param name="LastSyncAt">The last 'todo.note.synced', or null.</param>
/// <param name="LastError">The last failure's reason, or null.</param>
/// <param name="Conflicts">Ids of the todos with an open conflict, oldest first.</param>
/// <param name="Picks">The picks still in their Undo window.</param>
/// <param name="CantSync">The note lines that can't sync.</param>
public sealed record GetTodoNoteResponse(
    AsOf AsOf,
    string State,
    string? Vault,
    string? Note,
    DateTimeOffset? LastCheckAt,
    DateTimeOffset? LastSyncAt,
    string? LastError,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<TodoNotePick> Picks,
    IReadOnlyList<TodoNoteCantSync> CantSync);
