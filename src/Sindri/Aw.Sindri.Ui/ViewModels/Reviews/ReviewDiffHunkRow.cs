namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>A hunk's header row.</summary>
internal sealed class ReviewDiffHunkRow(ReviewDiffHunkViewModel hunk, bool isLast) : ReviewDiffRow(isLast)
{
    /// <inheritdoc />
    public override ReviewDiffRowKind Kind => ReviewDiffRowKind.Hunk;

    /// <summary>The hunk.</summary>
    public ReviewDiffHunkViewModel Hunk { get; } = hunk;
}
