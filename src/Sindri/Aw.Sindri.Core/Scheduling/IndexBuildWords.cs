using System.Globalization;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Core.Scheduling;

/// <summary>What the code index's build jobs say about a repo, shared by the doctor rows and the status read (code index spec §7.3 and §8): which jobs are the repo's, and why its next build waits.</summary>
public static class IndexBuildWords
{
    private const string BuildKind = "BuildCodeIndex";

    /// <summary>Why a repo's first index can never build (#3187): the Health card's look and the doctor row say it in these words (#4091).</summary>
    public const string Unbuildable = "can't build a code index: no default branch to build from (no origin/HEAD, main or master)";

    /// <summary>The fix for <see cref="Unbuildable"/>, the same on the Health card and the doctor row (#4091).</summary>
    public const string UnbuildableFix = "check out the default branch or fetch origin";

    /// <summary>A repo whose first index build runs: the doctor row's words, and the Health card's line for a repo with no commit says the same (#4091).</summary>
    public const string FirstBuildRunning = "building the first index";

    /// <summary>Whether the job is a code index build of the repo (key 'index:&lt;repo&gt;', lower case).</summary>
    /// <param name="job">The job.</param>
    /// <param name="repo">The repo's name.</param>
    /// <returns>True for the repo's build jobs.</returns>
    public static bool IsBuildOf(JobRow job, string repo)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job.Kind.Value == BuildKind && job.Key.Value == "index:" + repo.ToLowerInvariant();
    }

    /// <summary>'retrying in N min' after failures in a row still inside their backoff; null otherwise.</summary>
    /// <param name="finishedNewestFirst">The repo's ended build jobs, newest first.</param>
    /// <param name="now">The time.</param>
    /// <returns>The words, or null.</returns>
    public static string? Waiting(IReadOnlyList<JobRow> finishedNewestFirst, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(finishedNewestFirst);
        if (finishedNewestFirst is not [var newest, ..] || newest.EndedAt is not { } ended)
        {
            return null;
        }

        var failures = finishedNewestFirst.TakeWhile(static j => j.State == JobState.Failed).Count();
        var due = ended + SyncGapDecision.Wait(failures);
        return failures > 0 && now < due ? string.Create(CultureInfo.InvariantCulture, $"retrying in {Math.Ceiling((due - now).TotalMinutes)} min") : null;
    }
}
