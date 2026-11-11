namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The diff list's row for a file's header and note (#2776): the list is flat, so a virtualized panel builds only the rows in view.</summary>
/// <param name="file">The File the row shows.</param>
internal sealed class ReviewDiffFileRow(ReviewDiffFileViewModel file) : ReviewDiffRow
{
    /// <summary>The File the row shows; the row's content binds to it.</summary>
    public ReviewDiffFileViewModel File { get; } = file;
}
