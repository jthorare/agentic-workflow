namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/todos/note (todo spec §4.4): where the Obsidian note sync stands, the open conflicts, the open hold and the picks still in their Undo window. A view read.</summary>
/// <param name="AsOf">How fresh the view is.</param>
/// <param name="State">The doctor row's words: "not set", "waiting for import", "syncing", "one way", "can't read", "note busy".</param>
/// <param name="Vault">The vault of the note (todo.obsidian); null when not set.</param>
/// <param name="Note">The note's path in the vault; null when not set.</param>
/// <param name="LastCheckAt">The last 60 s check, or null.</param>
/// <param name="LastSyncAt">The last todo.note.synced, or null.</param>
/// <param name="LastError">The last failure's reason, or null.</param>
/// <param name="Conflicts">Ids of the todos with an open conflict, oldest first.</param>
/// <param name="Hold">The open hold (REQ-E7), or null.</param>
/// <param name="Picks">The picks still in their 10 s Undo window.</param>
/// <param name="CantSync">The note lines that can't sync.</param>
internal sealed record TodoNoteView(
    AsOfView AsOf,
    string State,
    string? Vault,
    string? Note,
    DateTimeOffset? LastCheckAt,
    DateTimeOffset? LastSyncAt,
    string? LastError,
    IReadOnlyList<string> Conflicts,
    TodoHoldView? Hold,
    IReadOnlyList<TodoPickView> Picks,
    IReadOnlyList<TodoCantSyncView> CantSync);
