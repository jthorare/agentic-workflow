namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>Where a node starts in the recipe file: 1-based line and column.</summary>
/// <param name="Line">The line, from 1.</param>
/// <param name="Column">The column, from 1.</param>
public sealed record SourcePosition(int Line, int Column)
{
    /// <summary>The start of the file.</summary>
    public static SourcePosition Start { get; } = new(1, 1);
}
