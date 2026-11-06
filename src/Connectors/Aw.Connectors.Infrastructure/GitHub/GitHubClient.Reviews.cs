using System.Globalization;
using System.Text.Json;
using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;
using Aw.Kernel;

namespace Aw.Connectors.Infrastructure.GitHub;

/// <summary>The review read of <see cref="GitHubClient"/>: GET /pulls/{n}/reviews, then one GET /compare per reviewed commit that is not the head.</summary>
internal sealed partial class GitHubClient
{
    // 10 pages of 100: a pull request with more than 1000 reviews is cut off here without a sign.
    private const int MaxReviewPages = 10;

    /// <inheritdoc />
    public async ValueTask<Result<GitHubReviewRead>> PullReviews(string repo, int number, string head, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(head);
        var root = await Prepare(repo, cancellationToken).ConfigureAwait(false);
        if (root.IsError)
        {
            return root.Error;
        }

        var listed = await ReadReviewList(new Uri(string.Create(CultureInfo.InvariantCulture, $"{Base(root.Value)}/repos/{repo}/pulls/{number}/reviews?per_page=100")), cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        if (listed.Value.RateLimitedUntil is { } until)
        {
            return new GitHubReviewRead([], until);
        }

        var latest = GitHubWire.LatestReviews(listed.Value.Reviews);
        Dictionary<string, int?> ahead = new(StringComparer.Ordinal) { [head] = 0 };
        foreach (var commit in latest.Select(static r => r.Commit).Distinct(StringComparer.Ordinal).Where(c => !ahead.ContainsKey(c)))
        {
            var compare = await Compare(Base(root.Value), repo, commit, head, cancellationToken).ConfigureAwait(false);
            if (compare.IsError)
            {
                return compare.Error;
            }

            if (compare.Value.RateLimitedUntil is { } limited)
            {
                return new GitHubReviewRead([], limited);
            }

            ahead[commit] = compare.Value.AheadBy;
        }

        return new GitHubReviewRead([.. latest.Select(r => new ConnectorReview(r.Login, r.Commit, ahead[r.Commit]))], RateLimitedUntil: null);
    }

    // A commit GitHub no longer has (a force-push dropped it) answers 404: the count is unknown, not an error.
    private async ValueTask<Result<(int? AheadBy, DateTimeOffset? RateLimitedUntil)>> Compare(string baseUrl, string repo, string commit, string head, CancellationToken cancellationToken)
    {
        var url = new Uri($"{baseUrl}/repos/{repo}/compare/{Uri.EscapeDataString(commit)}...{Uri.EscapeDataString(head)}?per_page=1");
        var step = await Fetch(url, cancellationToken).ConfigureAwait(false);
        if (step.IsError)
        {
            return string.Equals(step.Error.Code, ConnectorsErrors.NotFound.Code, StringComparison.Ordinal) ? (null, null) : step.Error;
        }

        return step.Value.RateLimitedUntil is { } until
            ? (null, until)
            : step.Value.Body is { ValueKind: JsonValueKind.Object } body
                ? (GitHubWire.AheadBy(body), null)
                : ConnectorsErrors.BadReply.With("GitHub's compare was not a JSON object.");
    }

    private async ValueTask<Result<(List<JsonElement> Reviews, DateTimeOffset? RateLimitedUntil)>> ReadReviewList(Uri first, CancellationToken cancellationToken)
    {
        List<JsonElement> reviews = [];
        var url = first;
        for (var page = 0; url is not null && page < MaxReviewPages; page++)
        {
            var step = await Fetch(url, cancellationToken).ConfigureAwait(false);
            if (step.IsError)
            {
                return step.Error;
            }

            if (step.Value.RateLimitedUntil is { } until)
            {
                return (reviews, until);
            }

            if (step.Value.Body is not { ValueKind: JsonValueKind.Array } array)
            {
                return ConnectorsErrors.BadReply.With("GitHub's review list was not a JSON array.");
            }

            reviews.AddRange(array.EnumerateArray());
            url = step.Value.Next;
        }

        return (reviews, null);
    }
}
