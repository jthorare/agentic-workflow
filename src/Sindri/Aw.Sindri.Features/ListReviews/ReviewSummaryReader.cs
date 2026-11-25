using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.ListReviews;

/// <summary>
/// The per-head summary behind a row's CI and open required checks (PR review page spec §6.1, §6.2 "Cache"). A head with a stored summary costs nothing; one without is read from the forge
/// (<see cref="IPullRequests.Get"/> for the check runs, <see cref="IPullRequests.Files"/> for the changed paths), at most <see cref="MaxHeadsAtOnce"/> at a time, decided and stored. A forge failure
/// stops reading new heads for this call (a rate limit, a rejected credential or an unreachable forge), except a PR the forge doesn't have, which skips only itself, and a repo the forge refuses (SND-FORGE-005), which skips that repo's PRs; a stored summary whose CI is still running is read again;
/// the open count leaves out the body: and manual: checks, which only the PR page can decide; each row then keeps its newest stored summary, even of an older head, or none.
/// </summary>
/// <param name="cache">The stored summaries.</param>
/// <param name="forge">The forge port.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">Warns once per repo per call when the forge refuses a repo, so a token without pull request access shows up somewhere.</param>
internal sealed partial class ReviewSummaryReader(IReviewSummaries cache, IPullRequests forge, TimeProvider clock, ILogger<ReviewSummaryReader> logger)
{
    /// <summary>The most heads read from the forge at once.</summary>
    internal const int MaxHeadsAtOnce = 4;

    /// <summary>Reads or builds the summary of each PR.</summary>
    /// <param name="pulls">The PRs the list shows.</param>
    /// <param name="review">The approved profile's review block, or null.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The summary by repo and number; a PR with none is left out.</returns>
    internal async Task<IReadOnlyDictionary<(string Repo, int Number), ReviewSummary>> Read(IReadOnlyList<ReviewPull> pulls, PrReviewSettings? review, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pulls);
        ConcurrentDictionary<(string Repo, int Number), ReviewSummary> found = new();
        var pass = new Pass(review);
        await Parallel.ForEachAsync(pulls, new ParallelOptions { MaxDegreeOfParallelism = MaxHeadsAtOnce, CancellationToken = cancellationToken }, async (pull, ct) =>
        {
            if (ReviewPullRef.TryParse(pull.Repo, pull.Number) is not { } reference)
            {
                return;
            }

            var summary = pull.HeadSha is { } head && ReviewPullRef.IsSha(head) ? await ForHead(pull, reference, head, pass, ct).ConfigureAwait(false) : null;
            summary ??= await Newest(reference, ct).ConfigureAwait(false);
            if (summary is not null)
            {
                found[(pull.Repo, pull.Number)] = summary;
            }
        }).ConfigureAwait(false);
        return found;
    }

    private async ValueTask<ReviewSummary?> Newest(ReviewPullRef reference, CancellationToken cancellationToken)
    {
        var newest = await cache.Newest(reference, cancellationToken).ConfigureAwait(false);
        return newest.IsError ? null : newest.Value;
    }

    private async ValueTask<ReviewSummary?> ForHead(ReviewPull pull, ReviewPullRef reference, string head, Pass pass, CancellationToken cancellationToken)
    {
        if (await Settled(reference, head, cancellationToken).ConfigureAwait(false) is { } hit)
        {
            return hit;
        }

        if (!pass.MayRead(pull.Repo) || pull.Url is not { } link)
        {
            return null;
        }

        var view = await forge.Get(link, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return Failed(pass, view.Error, pull.Repo);
        }

        // why: the stored record's head can lag the forge's; the summary is saved under the forge's head, so look there before reading the files
        if (!string.Equals(view.Value.HeadSha, head, StringComparison.OrdinalIgnoreCase) && await Settled(reference, view.Value.HeadSha, cancellationToken).ConfigureAwait(false) is { } current)
        {
            return current;
        }

        var files = await forge.Files(link, cancellationToken).ConfigureAwait(false);
        if (files.IsError)
        {
            return Failed(pass, files.Error, pull.Repo);
        }

        var summary = Decide(pull, view.Value, files.Value, pass.Review);
        _ = await cache.Write(reference, summary, cancellationToken).ConfigureAwait(false);
        return summary;
    }

    // why: until the list can show a refused repo (#3100), the log is the only place a token without pull request access is visible
    private ReviewSummary? Failed(Pass pass, Error error, string repo)
    {
        if (pass.Failed(error, repo))
        {
            LogRepoRefused(logger, repo, error.Message);
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-FORGE-005 the forge refused {Repo}; its PRs show no CI this pass: {Detail}")]
    private static partial void LogRepoRefused(ILogger logger, string repo, string detail);

    // why: a summary taken while CI was still running would show "running" until the next push, so only a finished one is a hit
    private async ValueTask<ReviewSummary?> Settled(ReviewPullRef reference, string head, CancellationToken cancellationToken)
    {
        var stored = await cache.Read(reference, head, cancellationToken).ConfigureAwait(false);
        return stored.IsSuccess && stored.Value is { Ci: not ReviewCi.Running } hit ? hit : null;
    }

    // why: the page owns the body: and manual: checks (the list has no PR body and no ticks), so the list's count leaves them out rather than freezing them as open per head
    private static List<PrReviewCheck> ListChecks(PrReviewSettings? review, string repo)
    {
        var settings = PrReviewDefaults.For(review, repo).Settings ?? PrReviewDefaults.Resolve(review, PrReviewDefaults.DefaultName);
        return [.. (settings?.Checks ?? []).Where(static c => c.Need?.Body is null && c.Need?.Manual != true)];
    }

    private ReviewSummary Decide(ReviewPull pull, PullView view, PullFiles files, PrReviewSettings? review)
    {
        var checks = ListChecks(review, pull.Repo);
        var runs = view.Checks.Runs ?? [];
        var inputs = new RequiredCheckInputs(
            checks, [.. files.Files.Select(static f => f.Path)], view.HeadSha, runs, Body: null, new HashSet<string>(StringComparer.Ordinal),
            pull.Claims.Count > 0 ? [.. pull.Claims.Select(static c => c.Ref)] : null, reference => pull.Claims.Any(c => c.Resolved && string.Equals(c.Ref, reference, StringComparison.Ordinal)));
        var ci = view.Checks.Runs is null ? FromState(view.Checks.State) : RequiredChecks.RollUp(runs, view.HeadSha);
        return new ReviewSummary(view.HeadSha, ci, checks.Count == 0 ? null : RequiredChecks.OpenCount(RequiredChecks.Evaluate(inputs)), clock.GetUtcNow());
    }

    private static ReviewCi FromState(PullChecksState state) => state switch
    {
        PullChecksState.Passing => ReviewCi.Pass,
        PullChecksState.Failing => ReviewCi.Fail,
        PullChecksState.Pending => ReviewCi.Running,
        PullChecksState.None => ReviewCi.None,
        _ => ReviewCi.None,
    };

    // why: one forge failure stops every later head read of the same call, whichever worker saw it
    private sealed class Pass(PrReviewSettings? review)
    {
        private readonly ConcurrentDictionary<string, bool> _refusedRepos = new(StringComparer.OrdinalIgnoreCase);
        private int _stopped;

        internal PrReviewSettings? Review { get; } = review;

        internal bool MayRead(string repo) => Volatile.Read(ref _stopped) == 0 && !_refusedRepos.ContainsKey(repo);

        // why: a missing PR is only that PR's problem; a repo the forge turns away is only that repo's; a bad credential, a rate limit or an unreachable forge would hit every later PR too
        // returns true only for the first refusal of a repo in this pass
        internal bool Failed(Error error, string repo)
        {
            if (error.Code == SindriErrors.ForgeRepoRefused.Code)
            {
                return _refusedRepos.TryAdd(repo, value: true);
            }

            if (error.Kind != ErrorKind.NotFound)
            {
                Volatile.Write(ref _stopped, 1);
            }

            return false;
        }
    }
}
