namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>A line row.</summary>
internal sealed class ReviewDiffLineRow(ReviewDiffLineViewModel line, bool isLast) : ReviewDiffRow(isLast)
{
    /// <inheritdoc />
    public override ReviewDiffRowKind Kind => ReviewDiffRowKind.Line;

    /// <summary>The line.</summary>
    public ReviewDiffLineViewModel Line { get; } = line;
}
