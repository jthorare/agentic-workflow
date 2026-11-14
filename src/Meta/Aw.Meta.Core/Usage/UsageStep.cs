namespace Aw.Meta.Core.Usage;

/// <summary>What one look at the subjects yields.</summary>
/// <param name="Lines">The watch lines to print.</param>
/// <param name="Memory">The memory afterwards.</param>
/// <param name="Resume">The lanes to type the resume line into now.</param>
/// <param name="Notes">The running-log notes to append.</param>
internal sealed record UsageStep(IReadOnlyList<string> Lines, UsageMemory Memory, IReadOnlyList<string> Resume, IReadOnlyList<string> Notes)
{
    /// <summary>The edges this look crossed: a subject that became limited, or whose window came back the first time.</summary>
    internal IReadOnlyList<UsageEdge> Edges { get; init; } = [];
}
