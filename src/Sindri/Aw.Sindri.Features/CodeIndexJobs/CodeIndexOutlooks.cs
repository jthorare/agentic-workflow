using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using IndexRepoHead = Aw.CodeIndex.Core.Model.RepoHead;

namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>
/// The code index's side of the map publish gate (code index spec §6.4): fills <see cref="IndexOutlookInput"/> per repo from the index's stored heads, the repos' live heads, the 'index:&lt;repo&gt;' jobs
/// (a failed job is a failure) and whether the 'code-index-main-move' service is paused, then classifies with <see cref="IndexOutlookRules"/>.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="index">The code index: its stored heads.</param>
/// <param name="heads">The repos' live heads.</param>
/// <param name="jobs">The 'jobs' view.</param>
/// <param name="pauses">The paused services.</param>
/// <param name="clock">The time.</param>
internal sealed class CodeIndexOutlooks(CodeIndexJobScopes scopes, ICodeIndex index, IRepoHeads heads, IJobViews jobs, IServicePauses pauses, TimeProvider clock) : IIndexOutlooks
{
    // The backoff caps at the fifth failure in a row and "broken" needs three, so the newest few jobs decide.
    private const int RecentJobs = 8;

    public async ValueTask<Result<IReadOnlyDictionary<string, IndexOutlookDetail>>> Read(MapHoldState hold, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hold);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var recorded = await index.Heads(scope.Value, cancellationToken).ConfigureAwait(false);
        var live = await heads.Heads(scope.Value.Repos, cancellationToken).ConfigureAwait(false);
        var builds = await jobs.List(state: null, IndexJobs.BuildKind, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError || live.IsError || builds.IsError)
        {
            return recorded.IsError ? recorded.Error : live.IsError ? live.Error : builds.Error;
        }

        var active = await MainMovePause.Active(pauses, cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        IReadOnlyDictionary<string, IndexOutlookDetail> all = hold.Heads.ToDictionary(
            static h => h.Key,
            h => IndexOutlookRules.Classify(Input(h.Key, h.Value, new Facts(scope.Value, recorded.Value, live.Value, builds.Value, active)), now),
            StringComparer.Ordinal);
        return Results.Success(all);
    }

    public async ValueTask<Result<IReadOnlyDictionary<string, string>>> IndexCommits(IReadOnlyList<string> repos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repos);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var recorded = await index.Heads(scope.Value, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            return recorded.Error;
        }

        IReadOnlyDictionary<string, string> commits = recorded.Value
            .Where(h => h.Commit is not null && repos.Contains(h.Repo.Value, StringComparer.Ordinal))
            .ToDictionary(static h => h.Repo.Value, static h => h.Commit?.Value ?? string.Empty, StringComparer.Ordinal);
        return Results.Success(commits);
    }

    private static IndexOutlookInput Input(string repo, MapRepoHead hold, Facts facts)
    {
        var (scope, recorded, live, builds, serviceActive) = facts;
        var stored = recorded.FirstOrDefault(h => string.Equals(h.Repo.Value, repo, StringComparison.Ordinal))?.Commit?.Value;
        var mine = builds.Where(j => IndexJobs.IsOf(j, repo)).ToList();
        var finished = mine.Where(static j => j.State != JobState.Running && j.EndedAt is not null).OrderByDescending(static j => j.EndedAt).Take(RecentJobs)
            .Select(static j => new IndexAttempt(j.State == JobState.Failed, j.EndedAt ?? default)).ToList();
        return new IndexOutlookInput(
            stored, hold.Commit, hold.OnDefaultBranch,
            live.FirstOrDefault(h => string.Equals(h.Repo.Value, repo, StringComparison.Ordinal))?.Head?.Value,
            scope.Repos.Any(r => string.Equals(r.Name.Value, repo, StringComparison.Ordinal)),
            serviceActive,
            BuildRunning: mine.Exists(static j => j.State == JobState.Running),
            NeverBuilt: stored is null && !mine.Exists(IndexJobs.Published),
            finished);
    }

    private sealed record Facts(CodeIndexScope Scope, IReadOnlyList<RecordedHead> Recorded, IReadOnlyList<IndexRepoHead> Live, IReadOnlyList<JobRow> Builds, bool ServiceActive);
}
