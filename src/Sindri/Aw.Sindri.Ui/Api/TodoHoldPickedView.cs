namespace Aw.Sindri.Ui.Api;

/// <summary>The answer of POST /sindri/todos/note/hold/apply and /restore (todo spec §4.4).</summary>
/// <param name="Kept">"applied" or "restored".</param>
/// <param name="Count">How many held changes the pick covers.</param>
/// <param name="UndoUntil">When the pick's Undo window closes, on Sindri's clock.</param>
internal sealed record TodoHoldPickedView(string? Kept = null, int? Count = null, DateTimeOffset? UndoUntil = null);
