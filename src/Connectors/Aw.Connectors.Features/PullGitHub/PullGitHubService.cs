using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Connectors.Features.PullGitHub;

/// <summary>
/// Reads the issue list, then the comments of the entries (one repo-wide read from the cursor to the newest entry, and one read for an entry whose
/// older comments the repo-wide read did not reach), each pull request's detail and, for an open one, who reviewed it and how far it has moved since (spec 5.6). Only the entries the cursor returns, the ones changed since the last read, are enriched, so an unchanged pull request costs no review or compare request. A rate limit in a later read cuts the run before the entry it hit, and the
/// cursor stops there, so the entry comes again complete.
/// </summary>
internal sealed class PullGitHubService(IGitHubReader reader) : IService<PullGitHubRequest, PullGitHubResponse>
{
    public async ValueTask<Result<PullGitHubResponse>> Handle(PullGitHubRequest request, CancellationToken cancellationToken)
    {
        var page = await reader.Issues(request.Repo, request.After, request.Limit, request.Before, cancellationToken).ConfigureAwait(false);
        if (page.IsError)
        {
            return page.Error;
        }

        var issues = page.Value.Items;
        var grouped = await RepoComments(request, issues, cancellationToken).ConfigureAwait(false);
        if (grouped.IsError)
        {
            return grouped.Error;
        }

        if (grouped.Value.Limited is { } limited)
        {
            return new PullGitHubResponse([], request.After, limited);
        }

        List<GitHubIssue> done = [];
        List<ConnectorItem> items = [];
        foreach (var issue in issues)
        {
            var one = await Enrich(request, issue, grouped.Value.Grouped, cancellationToken).ConfigureAwait(false);
            if (one.IsError)
            {
                return one.Error;
            }

            if (one.Value.Stopped is { } until)
            {
                return new PullGitHubResponse(items, request.After.Advance(done), until);
            }

            done.Add(issue);
            items.Add(GitHubRecords.From(request.Repo, issue, one.Value.Comments, one.Value.Detail, one.Value.Reviews));
        }

        return new PullGitHubResponse(items, page.Value.Next, page.Value.RateLimitedUntil);
    }

    /// <summary>The repo-wide comments grouped by entry number (empty when none are wanted), or the reset time when the rate limit stopped the read.</summary>
    private async ValueTask<Result<RepoCommentRead>> RepoComments(PullGitHubRequest request, IReadOnlyList<GitHubIssue> issues, CancellationToken cancellationToken)
    {
        if (request.CommentCap == 0 || !issues.Any(static i => i.Comments > 0))
        {
            return new RepoCommentRead([], Limited: null);
        }

        var read = await reader.Comments(request.Repo, request.After.UpdatedAt, issues.Max(static i => i.UpdatedAt), cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        return read.Value.RateLimitedUntil is { } limited
            ? new RepoCommentRead([], limited)
            : new RepoCommentRead(read.Value.Items.GroupBy(static c => c.IssueNumber).ToDictionary(static g => g.Key, static g => g.ToList()), Limited: null);
    }

    /// <summary>The repo-wide comment read: the comments by entry number, or when the rate limit stopped it, its reset time.</summary>
    private sealed record RepoCommentRead(Dictionary<int, List<GitHubComment>> Grouped, DateTimeOffset? Limited);

    private async ValueTask<Result<Enriched>> Enrich(PullGitHubRequest request, GitHubIssue issue, Dictionary<int, List<GitHubComment>> grouped, CancellationToken cancellationToken)
    {
        var comments = grouped.TryGetValue(issue.Number, out var found) ? found : [];
        if (comments.Count < Math.Min(issue.Comments, request.CommentCap))
        {
            var all = await reader.IssueComments(request.Repo, issue.Number, cancellationToken).ConfigureAwait(false);
            if (all.IsError)
            {
                return all.Error;
            }

            if (all.Value.RateLimitedUntil is { } limited)
            {
                return new Enriched([], Detail: null, [], limited);
            }

            comments = [.. all.Value.Items];
        }

        var newest = comments.OrderByDescending(static c => c.CreatedAt).ThenByDescending(static c => c.Id).Take(request.CommentCap);
        var talk = new Enriched([.. newest.OrderBy(static c => c.CreatedAt).ThenBy(static c => c.Id)], Detail: null, [], Stopped: null);
        return issue.IsPullRequest ? await PullFacts(request, issue, talk, cancellationToken).ConfigureAwait(false) : talk;
    }

    // The single-pull-request read, then for an open one with a head the review read; a rate limit in either cuts the entry (nothing of it is kept).
    private async ValueTask<Result<Enriched>> PullFacts(PullGitHubRequest request, GitHubIssue issue, Enriched talk, CancellationToken cancellationToken)
    {
        var pull = await reader.PullRequest(request.Repo, issue.Number, cancellationToken).ConfigureAwait(false);
        if (pull.IsError)
        {
            return pull.Error;
        }

        if (pull.Value.RateLimitedUntil is { } limited)
        {
            return new Enriched([], Detail: null, [], limited);
        }

        var detail = pull.Value.Detail;
        if (detail is not { Merged: false, HeadSha: { Length: > 0 } head } || !string.Equals(issue.State, "open", StringComparison.Ordinal))
        {
            return talk with { Detail = detail };
        }

        var read = await reader.PullReviews(request.Repo, issue.Number, head, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        return read.Value.RateLimitedUntil is { } stopped
            ? new Enriched([], Detail: null, [], stopped)
            : talk with { Detail = detail, Reviews = read.Value.Reviews };
    }

    private sealed record Enriched(IReadOnlyList<GitHubComment> Comments, GitHubPullDetail? Detail, IReadOnlyList<ConnectorReview> Reviews, DateTimeOffset? Stopped);
}
