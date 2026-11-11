namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The diff list's row for a group's heading (#2776): the list is flat, so a virtualized panel builds only the rows in view.</summary>
/// <param name="group">The Group the row shows.</param>
internal sealed class ReviewDiffGroupRow(ReviewDiffGroupViewModel group) : ReviewDiffRow
{
    /// <summary>The Group the row shows; the row's content binds to it.</summary>
    public ReviewDiffGroupViewModel Group { get; } = group;
}
