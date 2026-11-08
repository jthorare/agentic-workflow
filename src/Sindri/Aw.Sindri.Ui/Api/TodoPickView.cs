namespace Aw.Sindri.Ui.Api;

/// <summary>A pick still in its 10 s Undo window (todo spec D11).</summary>
/// <param name="Kind">"conflict" or "hold".</param>
/// <param name="Id">The todo's id for a conflict pick; null for a hold's.</param>
/// <param name="Kept">"sindri" or "obsidian" for a conflict; "applied" or "restored" for a hold.</param>
/// <param name="By">Who picked: "human:ui", "session:…".</param>
/// <param name="UndoUntil">When the window closes, on Sindri's clock.</param>
internal sealed record TodoPickView(string Kind, string? Id, string Kept, string By, DateTimeOffset UndoUntil);
