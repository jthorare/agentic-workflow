namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>A group's header row.</summary>
internal sealed class ReviewDiffGroupRow(ReviewDiffGroupViewModel group) : ReviewDiffRow(isLast: false)
{
    /// <inheritdoc />
    public override ReviewDiffRowKind Kind => ReviewDiffRowKind.Group;

    /// <summary>The group.</summary>
    public ReviewDiffGroupViewModel Group { get; } = group;
}
