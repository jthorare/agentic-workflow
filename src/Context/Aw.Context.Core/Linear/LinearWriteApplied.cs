namespace Aw.Context.Core.Linear;

/// <summary>Linear applied the write and the answer is stored: the stored issue is what a page reads next.</summary>
/// <param name="Stored">The issue as written through to the store.</param>
public sealed record LinearWriteApplied(LinearIssueView Stored) : LinearWriteOutcome
{
    /// <summary>True when Linear gave no answer to the send and only the re-read found the write; false when Linear answered it directly.</summary>
    public bool FoundOnReread { get; init; }
}
