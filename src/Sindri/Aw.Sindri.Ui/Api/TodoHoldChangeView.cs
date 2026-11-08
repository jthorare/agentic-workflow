namespace Aw.Sindri.Ui.Api;

/// <summary>One held change of a hold (todo spec §4.4).</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Kind">"removed", "edited" or "reopened".</param>
/// <param name="Text">Sindri's text for the todo.</param>
/// <param name="NoteLine">The note's line; null for a removal.</param>
internal sealed record TodoHoldChangeView(string Id, string Kind, string Text, string? NoteLine);
