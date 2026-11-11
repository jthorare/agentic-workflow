namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The diff list's row for a hunk's @@ header (#2776): the list is flat, so a virtualized panel builds only the rows in view.</summary>
/// <param name="hunk">The Hunk the row shows.</param>
internal sealed class ReviewDiffHunkRow(ReviewDiffHunkViewModel hunk) : ReviewDiffRow
{
    /// <summary>The Hunk the row shows; the row's content binds to it.</summary>
    public ReviewDiffHunkViewModel Hunk { get; } = hunk;
}
