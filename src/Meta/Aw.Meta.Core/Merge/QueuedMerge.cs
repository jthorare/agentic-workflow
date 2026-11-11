namespace Aw.Meta.Core.Merge;

/// <summary>A pull request merge-run handed to GitHub's merge queue and the watch has not seen end yet.</summary>
/// <param name="Pr">The pull request number.</param>
/// <param name="At">When merge-run enqueued it.</param>
/// <param name="Head">The head sha it was enqueued at.</param>
public sealed record QueuedMerge(int Pr, DateTimeOffset At, string Head);
