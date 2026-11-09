namespace Aw.Sindri.Features.GetTodoNote;

/// <summary>A pick still inside its 10 s Undo window (todo spec §4.4).</summary>
/// <param name="Kind">'conflict' or 'hold'.</param>
/// <param name="Id">The todo, for a conflict pick.</param>
/// <param name="Kept">'sindri' or 'obsidian' for a conflict; 'applied' or 'restored' for a hold.</param>
/// <param name="By">Who picked.</param>
/// <param name="UndoUntil">When the window closes.</param>
public sealed record TodoNotePick(string Kind, Core.Ids.TodoId? Id, string Kept, string By, DateTimeOffset UndoUntil);
