namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The diff list's row for one diff line (#2776): the list is flat, so a virtualized panel builds only the rows in view.</summary>
/// <param name="line">The Line the row shows.</param>
internal sealed class ReviewDiffLineRow(ReviewDiffLineViewModel line) : ReviewDiffRow
{
    /// <summary>The Line the row shows; the row's content binds to it.</summary>
    public ReviewDiffLineViewModel Line { get; } = line;
}
