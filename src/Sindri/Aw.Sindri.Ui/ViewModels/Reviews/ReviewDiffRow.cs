namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>
/// One row of the diff as one flat list (#2776): group headers, file headers, hunk headers and lines in reading order, so a virtualizing panel builds only
/// the rows in view. A file's card is drawn piece by piece: the file row opens it, <see cref="IsLast"/> closes it.
/// </summary>
internal abstract class ReviewDiffRow(bool isLast)
{
    /// <summary>What the row draws.</summary>
    public abstract ReviewDiffRowKind Kind { get; }

    /// <summary>True for the last row of a file's card: it draws the card's bottom edge and the gap to the next file.</summary>
    public bool IsLast { get; } = isLast;
}
