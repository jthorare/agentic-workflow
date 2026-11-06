using System.Collections.Concurrent;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.ListReviews;

/// <summary>
/// The per-head summary behind a row's CI and open required checks (PR review page spec §6.1, §6.2 "Cache"). A head with a stored summary costs nothing; one without is read from the forge
/// (<see cref="IPullRequests.Get"/> for the check runs, <see cref="IPullRequests.Files"/> for the changed paths), at most <see cref="MaxHeadsAtOnce"/> at a time, decided and stored. A forge failure
/// of any kind (a 403 or 429 rate limit too) stops reading new heads for this call; each row then keeps its newest stored summary, even of an older head, or none.
/// </summary>
/// <param name="cache">The stored summaries.</param>
/// <param name="forge">The forge port.</param>
/// <param name="clock">The clock.</param>
internal sealed class ReviewSummaryReader(IReviewSummaries cache, IPullRequests forge, TimeProvider clock)
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
        var stored = await cache.Read(reference, head, cancellationToken).ConfigureAwait(false);
        if (stored.IsSuccess && stored.Value is { } hit)
        {
            return hit;
        }

        if (!pass.MayRead || pull.Url is not { } link)
        {
            return null;
        }

        var view = await forge.Get(link, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            pass.Stop();
            return null;
        }

        var files = await forge.Files(link, cancellationToken).ConfigureAwait(false);
        if (files.IsError)
        {
            pass.Stop();
            return null;
        }

        var summary = Decide(pull, view.Value, files.Value, pass.Review);
        _ = await cache.Write(reference, summary, cancellationToken).ConfigureAwait(false);
        return summary;
    }

    private ReviewSummary Decide(ReviewPull pull, PullView view, PullFiles files, PrReviewSettings? review)
    {
        var settings = PrReviewDefaults.For(review, pull.Repo).Settings ?? PrReviewDefaults.Resolve(review, PrReviewDefaults.DefaultName);
        var checks = settings?.Checks ?? [];
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
        private int _stopped;

        internal PrReviewSettings? Review { get; } = review;

        internal bool MayRead => Volatile.Read(ref _stopped) == 0;

        internal void Stop() => Volatile.Write(ref _stopped, 1);
    }
}
