namespace Aw.Sindri.Ui.Api;

/// <summary>The answer of POST /sindri/todos/note/hold/undo (todo spec §4.4): the hold is open again.</summary>
/// <param name="Kept">Null.</param>
internal sealed record TodoHoldUndoneView(string? Kept = null);
