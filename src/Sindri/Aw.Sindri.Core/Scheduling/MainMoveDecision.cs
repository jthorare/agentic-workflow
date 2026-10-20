namespace Aw.Sindri.Core.Scheduling;

/// <summary>
/// The pure decision of a service that acts when a repo's checkout moves along its default branch (owner F1: the context map refresh now, the code index build later; each repo compared by name).
/// A running job first, then nothing when no repo is on its default branch or none moved since the last success, then the backoff after failures in a row
/// (the same 5, 10, 20, 40, then 60 minutes as <see cref="SyncGapDecision"/>, counted from the newest failure; any other outcome resets it).
/// A repo that was removed or left its default branch is no reason to start by itself; one that was added or moved is.
/// </summary>
public static class MainMoveDecision
{
    /// <summary>Decides.</summary>
    /// <param name="onDefaultHeads">The HEAD of each repo whose checkout is on its default branch, by repo name.</param>
    /// <param name="lastSucceededHeads">The heads the last successful job read, or null when none has succeeded yet.</param>
    /// <param name="running">Whether a job of the kind is running.</param>
    /// <param name="finishedNewestFirst">The ended jobs, newest first.</param>
    /// <param name="now">The time.</param>
    /// <returns>The action.</returns>
    public static MainMoveAction Decide(IReadOnlyDictionary<string, string> onDefaultHeads, IReadOnlyDictionary<string, string>? lastSucceededHeads, bool running, IReadOnlyList<SyncAttempt> finishedNewestFirst, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(onDefaultHeads);
        ArgumentNullException.ThrowIfNull(finishedNewestFirst);
        if (running)
        {
            return MainMoveAction.JobRunning;
        }

        if (onDefaultHeads.Count == 0 || !Moved(onDefaultHeads, lastSucceededHeads))
        {
            return MainMoveAction.Nothing;
        }

        var failures = FailedInARow(finishedNewestFirst);
        return failures > 0 && now < finishedNewestFirst[0].EndedAt + SyncGapDecision.Wait(failures) ? MainMoveAction.BackoffNotDue : MainMoveAction.Start;
    }

    /// <summary>The failed jobs at the head of the list, up to the first one that did not fail.</summary>
    /// <param name="finishedNewestFirst">The ended jobs, newest first.</param>
    /// <returns>The count.</returns>
    public static int FailedInARow(IReadOnlyList<SyncAttempt> finishedNewestFirst)
    {
        ArgumentNullException.ThrowIfNull(finishedNewestFirst);
        return finishedNewestFirst.TakeWhile(static a => a.Failed).Count();
    }

    private static bool Moved(IReadOnlyDictionary<string, string> current, IReadOnlyDictionary<string, string>? last) =>
        last is null || current.Any(h => !last.TryGetValue(h.Key, out var sha) || !string.Equals(sha, h.Value, StringComparison.Ordinal));
}
