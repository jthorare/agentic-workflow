using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Features.SendReviewFix;

/// <summary>Posts a fix task's finding as a PR thread through <see cref="IPullRequests.SubmitReview"/> (PR review page spec §8.2 step 2); a failure is the half-done send, never an error.</summary>
/// <param name="pulls">The forge port.</param>
internal sealed class FixThreadPoster(IPullRequests pulls)
{
    /// <summary>Posts the review once (the marker makes a retry post nothing twice).</summary>
    /// <param name="review">The review.</param>
    /// <param name="item">The item's id, for the half-done text.</param>
    /// <param name="number">The PR number, for the half-done text.</param>
    /// <param name="cancellationToken">Cancels the post.</param>
    /// <returns>How it ended.</returns>
    internal async ValueTask<FixPosted> Post(PullReview review, string item, int number, CancellationToken cancellationToken)
    {
        var outcome = await pulls.SubmitReview(review, cancellationToken).ConfigureAwait(false);
        if (outcome.IsError)
        {
            return Failed(item, number, outcome.Error.Message);
        }

        return outcome.Value switch
        {
            PullReviewOutcome.Posted or PullReviewOutcome.AlreadyDelivered => new FixPosted(Posted: true, Code: null, Error: null, await ThreadLink(review, cancellationToken).ConfigureAwait(false)),
            PullReviewOutcome.HeadMoved => Failed(item, number, "the PR's head moved since the finding was written"),
            PullReviewOutcome.LineRejected => Failed(item, number, "GitHub refused the comment on that line"),
            _ => Failed(item, number, "GitHub didn't take the comment"),
        };
    }

    private static FixPosted Failed(string item, int number, string reason) =>
        new(Posted: false, SindriErrors.ReviewFixNotPosted.Code, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Item {item} created in Work; posting to #{number} failed: {reason}"), Thread: null);

    // why: SubmitReview says only that it posted; the thread's link is for the event, so any failure to find it is a null, never a failed send
    private async ValueTask<string?> ThreadLink(PullReview review, CancellationToken cancellationToken)
    {
        var threads = await pulls.Threads(review.Link, cancellationToken).ConfigureAwait(false);
        return threads.IsError ? null : threads.Value.FirstOrDefault(t => t.Markers.Contains(review.Marker, StringComparer.Ordinal) || t.Body.Contains(review.Marker, StringComparison.Ordinal))?.Url;
    }
}
