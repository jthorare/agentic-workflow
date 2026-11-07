namespace Aw.Sindri.Features.SendReviewFix;

/// <summary>What posting a fix thread came to.</summary>
/// <param name="Posted">Whether the thread is on the PR now.</param>
/// <param name="Code">SND-REVIEW-006 when it is not; null otherwise.</param>
/// <param name="Error">The half-done text when it is not; null otherwise.</param>
/// <param name="Thread">The thread's link when it could be found; null otherwise.</param>
internal sealed record FixPosted(bool Posted, string? Code, string? Error, string? Thread)
{
    /// <summary>Nothing was posted on purpose: a contributor's PR.</summary>
    internal static FixPosted Skipped { get; } = new(Posted: false, Code: null, Error: null, Thread: null);
}
