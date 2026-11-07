using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetReviewPage;

/// <summary>
/// The PR page's cache (spec §6.2 "Cache"): the page snapshot per head, and the head's list summary that the Reviews list reads (so a page read refreshes the row it came from).
/// When the forge can't be reached the newest cached page answers with an offline note; with none cached the answer is SND-REVIEW-503.
/// </summary>
/// <param name="pages">The page snapshots.</param>
/// <param name="summaries">The per-head summaries.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="clock">The clock.</param>
internal sealed class ReviewPageCache(IReviewPages pages, IReviewSummaries summaries, IProjectorStatus status, TimeProvider clock)
{
    internal async ValueTask Save(ReviewPullRef pull, ReviewPageModel page, ReviewPageFacts facts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(facts);
        _ = await pages.Write(pull, page, cancellationToken).ConfigureAwait(false);
        _ = await summaries.Write(pull, Summary(page, facts), cancellationToken).ConfigureAwait(false);
    }

    internal async ValueTask<Result<ReviewPageModel>> Offline(ReviewPullRef pull, CancellationToken cancellationToken)
    {
        var cached = await pages.Newest(pull, cancellationToken).ConfigureAwait(false);
        return cached.IsSuccess && cached.Value is { } hit
            ? hit.Page with { AsOf = status.Current.AsOf, Offline = new ReviewPageOffline(hit.CachedAt, "as of " + hit.CachedAt.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture) + ", the PR may have moved") }
            : SindriErrors.ReviewOfflineUncached.With("Offline, and this PR isn't cached yet");
    }

    private ReviewSummary Summary(ReviewPageModel page, ReviewPageFacts facts)
    {
        var checks = page.Panels.FirstOrDefault(static p => string.Equals(p.Key, "requiredChecks", StringComparison.Ordinal)) is { Data: RequiredChecksData data } ? data.Checks : null;
        var ci = facts.Runs.Ok ? RequiredChecks.RollUp(facts.Runs.Value, facts.Pr.HeadSha) : ReviewCi.None;
        return new ReviewSummary(facts.Pr.HeadSha, ci, checks?.Count(static c => c.Status is not ("pass" or "ticked")), clock.GetUtcNow());
    }
}
