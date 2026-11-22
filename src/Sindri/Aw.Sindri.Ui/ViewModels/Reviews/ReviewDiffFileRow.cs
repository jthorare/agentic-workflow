namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>A file's header row: the top of its card.</summary>
internal sealed class ReviewDiffFileRow(ReviewDiffFileViewModel file, bool isLast) : ReviewDiffRow(isLast)
{
    /// <inheritdoc />
    public override ReviewDiffRowKind Kind => ReviewDiffRowKind.File;

    /// <summary>The file.</summary>
    public ReviewDiffFileViewModel File { get; } = file;
}
