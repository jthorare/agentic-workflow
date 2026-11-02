namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>What a pinned card's footer says (concept pins spec §3.7): up to three lines and the one button.</summary>
/// <param name="Lines">The state line, the first problem line, the what-moved line; each only when there is one.</param>
/// <param name="Action">The button the footer carries.</param>
internal sealed record PinCardLines(IReadOnlyList<string> Lines, PinCardAction Action);
