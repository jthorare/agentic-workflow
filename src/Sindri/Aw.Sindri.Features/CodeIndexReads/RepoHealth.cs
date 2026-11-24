using System.Globalization;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using IndexRepoHead = Aw.CodeIndex.Core.Model.RepoHead;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>
/// A repo's index state as the Health card words it (code index spec §7.3): the facade's file state with 'stale', 'building', 'waiting' and 'queued' laid over it from the live head and the build jobs, and the embeddings
/// layer's progress as a percent. Counts and words only.
/// </summary>
/// <param name="Repo">The repo.</param>
/// <param name="State">missing, damaged, ok, stale (the head moved past the index), building (a build job runs), waiting (a failed build holds the next one) queued (no index yet, the head is on the default branch or the checkout has a default-branch tip to build from, and code-index-main-move is not paused, so it starts the first build on its next tick; #3342, #3399, #3181) or unbuildable (no index yet, the head off its default branch and no default branch to build from, so no first build can ever run; #3187).</param>
/// <param name="EmbeddingsPercent">The share of embeddable symbols that have a vector, or null when the layer states no count.</param>
/// <param name="Waiting">What the next build waits for: 'retrying in N min' while the state is waiting, <see cref="QueuedWords"/> while it is queued and needs no look (#3515), else null.</param>
public sealed record RepoHealth(string Repo, string State, int? EmbeddingsPercent, string? Waiting)
{
    /// <summary>The waiting words of a queued repo (#3515): code-index-main-move ticks every minute and starts the first build on its next tick.</summary>
    public const string QueuedWords = "the first build starts on code-index-main-move's next tick";

    /// <summary>The waiting words of a repo queued behind another repo's running first build (the first-builds queue, #3322): code-index-main-move builds one first index at a time.</summary>
    /// <param name="holder">The repo whose first build runs.</param>
    /// <returns>The words.</returns>
    public static string QueuedBehindWords(string holder) => $"the first build starts after {holder}'s first build";

    /// <summary>How long this serve has read the repo as queued, or null when it isn't queued; set by RepoHealths from its <see cref="QueuedClock"/>. Internal, so it isn't in the API answer (#3440).</summary>
    internal TimeSpan? QueuedFor { get; init; }

    /// <summary>True when code-index-main-move is paused and this repo has no build coming (no index, behind main or in backoff): the card says so and names the resume, as doctor does (#3561). Set by <see cref="Unqueued"/>.</summary>
    public bool Paused { get; init; }

    // The states that wait on a build; paused, none comes.
    private static readonly string[] _noBuildComing = ["queued", "missing", "stale"];

    // The state without the waiting word: what the repo is while no retry is coming (stale, missing or queued).
    private string? _beneathWaiting;

    /// <summary>True when doctor has a problem row for the repo (issue #3430): the System card reads this, not the state word, so a new busy state needs no app change.</summary>
    public bool NeedsLook { get; init; }

    /// <summary>Doctor's words for the problem that set <see cref="NeedsLook"/> ("demo: code index is behind main; code-index-main-move rebuilds it on its next tick"), or null when the repo needs no look (#3515): the app shows these on the repo's line, so it keeps no list of its own.</summary>
    public string? LookWords { get; init; }

    /// <summary>Lays the heads and jobs over one repo's status.</summary>
    /// <param name="status">The repo's index status, from the files.</param>
    /// <param name="live">The repo's live head, or null when unread.</param>
    /// <param name="builds">The repo's build jobs.</param>
    /// <param name="now">The time.</param>
    /// <returns>The health.</returns>
    public static RepoHealth Of(RepoStatus status, IndexRepoHead? live, IReadOnlyList<JobRow> builds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(builds);
        return Of(status, live, builds, builds, now);
    }

    /// <summary>Lays the heads and jobs over one repo's status, with the ended builds read apart from the running ones (issue #2786).</summary>
    /// <param name="status">The repo's index status, from the files.</param>
    /// <param name="live">The repo's live head, or null when unread.</param>
    /// <param name="running">The repo's running build jobs.</param>
    /// <param name="ended">The repo's ended build jobs; read only when <see cref="NeedsEnded"/> says so, otherwise it may be empty.</param>
    /// <param name="now">The time.</param>
    /// <returns>The health.</returns>
    public static RepoHealth Of(RepoStatus status, IndexRepoHead? live, IReadOnlyList<JobRow> running, IReadOnlyList<JobRow> ended, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(running);
        ArgumentNullException.ThrowIfNull(ended);
        var percent = Percent(status.Layers.FirstOrDefault(static l => l.Layer == LayerName.Embeddings)?.Detail);
        var (state, waiting) = Overlay(status, live, running, ended, now);
        return new RepoHealth(status.Repo.Value, state, percent, waiting) { _beneathWaiting = waiting is null ? null : BeneathWaiting(status, live) };
    }

    /// <summary>The health read again against code-index-main-move: a paused service starts no build and no retry, so 'queued' is missing, 'waiting' is what the repo is beneath it, and <see cref="Paused"/> is set for a repo that waits on a build (#3399, #3561).</summary>
    /// <param name="buildServiceActive">Whether code-index-main-move runs.</param>
    /// <returns>The health.</returns>
    internal RepoHealth Unqueued(bool buildServiceActive)
    {
        if (buildServiceActive)
        {
            return this;
        }

        var state = State == "waiting" ? _beneathWaiting ?? "stale" : State;
        return _noBuildComing.Contains(state, StringComparer.Ordinal)
            ? this with { State = state == "queued" ? "missing" : state, Waiting = null, Paused = true }
            : this;
    }

    /// <summary>Whether the repo's health words need its ended builds: the index isn't damaged, no build runs, and the repo is behind or has a pending layer.</summary>
    /// <param name="status">The repo's index status.</param>
    /// <param name="live">The repo's live head, or null when unread.</param>
    /// <param name="running">The repo's running build jobs.</param>
    /// <returns>True when <see cref="Of(RepoStatus, IndexRepoHead?, IReadOnlyList{JobRow}, IReadOnlyList{JobRow}, DateTimeOffset)"/> reads the ended builds.</returns>
    public static bool NeedsEnded(RepoStatus status, IndexRepoHead? live, IReadOnlyList<JobRow> running)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(running);
        return status.State != IndexState.Damaged && !running.Any(static j => j.State == JobState.Running) && Behind(status, live);
    }

    private static bool Behind(RepoStatus status, IndexRepoHead? live) =>
        status.State == IndexState.Missing || Moved(status, live) || status.Layers.Any(static l => l.State == LayerState.Pending);

    private static (string State, string? Waiting) Overlay(RepoStatus status, IndexRepoHead? live, IReadOnlyList<JobRow> running, IReadOnlyList<JobRow> ended, DateTimeOffset now)
    {
        if (status.State == IndexState.Damaged)
        {
            return ("damaged", null);
        }

        if (running.Any(static j => j.State == JobState.Running))
        {
            return ("building", null);
        }

        var missing = status.State == IndexState.Missing;
        var behind = missing || Moved(status, live);
        var waiting = Behind(status, live) ? WaitingWords(ended, now) : null;
        return (Word(waiting, missing, behind, live), waiting);
    }

    private static string BeneathWaiting(RepoStatus status, IndexRepoHead? live)
    {
        var missing = status.State == IndexState.Missing;
        return Word(waiting: null, missing, behind: missing || Moved(status, live), live);
    }

    private static bool Moved(RepoStatus status, IndexRepoHead? live) => live?.Head is { } head && head != status.Commit;

    private static string Word(string? waiting, bool missing, bool behind, IndexRepoHead? live) =>
        waiting is not null ? "waiting" : missing ? MissingWord(live) : behind ? "stale" : "ok";

    // A missing index: queued when a first build comes, unbuildable when none ever can (off the default branch, no default tip; #3187), else missing.
    private static string MissingWord(IndexRepoHead? live) =>
        FirstBuildComes(live) ? "queued" : live is { Head: not null, OnDefaultBranch: false, DefaultTip: null } ? "unbuildable" : "missing";

    // why: off the default branch the first build runs at the default branch's tip (#3181), so a repo with one is queued too
    private static bool FirstBuildComes(IndexRepoHead? live) => live is { Head: not null } && (live.OnDefaultBranch || live.DefaultTip is not null);

    private static string? WaitingWords(IReadOnlyList<JobRow> builds, DateTimeOffset now) =>
        IndexBuildWords.Waiting([.. builds.Where(static j => j.EndedAt is not null).OrderByDescending(static j => j.EndedAt)], now);

    // The embeddings layer's detail is 'N of M embedded' (spec §5.5): anything else states no count.
    private static int? Percent(string? detail)
    {
        var parts = detail?.Split(separator: ' ');
        return parts is [var done, "of", var all, "embedded"] && int.TryParse(done, style: NumberStyles.None, provider: CultureInfo.InvariantCulture, result: out var n) && int.TryParse(all, style: NumberStyles.None, provider: CultureInfo.InvariantCulture, result: out var m)
            ? m == 0 ? 100 : (int)Math.Min(100L, 100L * n / m)
            : null;
    }
}
