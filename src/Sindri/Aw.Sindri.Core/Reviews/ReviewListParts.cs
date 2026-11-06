namespace Aw.Sindri.Core.Reviews;

/// <summary>The rows of the Reviews list, grouped, with what is decided from them.</summary>
/// <param name="Groups">Waiting on you, then Yours.</param>
/// <param name="WaitingCount">The Waiting group's size.</param>
/// <param name="Notice">The line above the list while reviewers were never read; null otherwise.</param>
/// <param name="Empty">The empty state when there are no rows; null otherwise.</param>
public sealed record ReviewListParts(IReadOnlyList<ReviewGroup> Groups, int WaitingCount, string? Notice, ReviewEmpty? Empty);
