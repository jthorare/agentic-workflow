namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>What <see cref="PinListParser"/> read from a pasted list.</summary>
/// <param name="Lines">The concepts, at most <see cref="PinListParser.MaxLines"/>.</param>
/// <param name="Truncated">Whether the list had more concepts than that and the rest was left out.</param>
#pragma warning disable MA0182 // Q7b: the Add several view model and its tests use it
internal sealed record PinListRead(IReadOnlyList<PinListLine> Lines, bool Truncated);
#pragma warning restore MA0182
