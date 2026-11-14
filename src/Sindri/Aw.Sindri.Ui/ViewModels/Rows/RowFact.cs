namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>One fact of a row, shown in the list column with the same key.</summary>
/// <param name="Kind">How the cell is drawn.</param>
/// <param name="Text">The cell's text (the name, the ref, the count).</param>
/// <param name="Done">For <see cref="RowFactKind.Progress"/>, the part done.</param>
/// <param name="Total">For <see cref="RowFactKind.Progress"/>, the whole.</param>
/// <param name="Tone">For <see cref="RowFactKind.Chip"/>, the colour. Set by the adapter; never guessed from <paramref name="Text"/>.</param>
internal sealed record RowFact(RowFactKind Kind, string Text, int Done = 0, int Total = 0, RowTone Tone = RowTone.Neutral);
