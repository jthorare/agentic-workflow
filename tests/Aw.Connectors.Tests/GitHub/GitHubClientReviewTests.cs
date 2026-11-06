using System.Net;
using Aw.Connectors.Core;
using Aw.Connectors.Infrastructure.GitHub;
using Aw.Connectors.Testing.GitHub;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Connectors.Tests.GitHub;

/// <summary>PR review page spec §5.6 and D21: the review read and the one compare per reviewed commit that is not the head.</summary>
public sealed class GitHubClientReviewTests : IDisposable
{
    private const string Repo = "acme/widgets";
    private static readonly DateTimeOffset _t = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeGitHubServer _server = new();

    public void Dispose() => _server.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private GitHubClient Client() => new(new HttpClient(_server), new GitHubClientSettings(), new PublicResolver(), new FakeTimeProvider(new DateTimeOffset(2026, 11, 5, 9, 0, 0, TimeSpan.Zero)));

    private string[] Paths() => [.. _server.Requests.Select(static r => r.Uri.AbsolutePath)];

    [Fact, Trait("Lane", "unit")]
    public async Task A_reviewer_whose_commit_is_behind_the_head_gets_the_compares_ahead_by()
    {
        _ = _server.AddReview(Repo, 12, "bob", "APPROVED", "aaa", _t).AddCompare(Repo, "aaa", "ccc", 2);

        var read = (await Client().PullReviews(Repo, 12, "ccc", Ct)).Value;

        Assert.Equal([new ConnectorReview("bob", "aaa", 2)], read.Reviews);
        Assert.Equal(["/repos/acme/widgets/pulls/12/reviews", "/repos/acme/widgets/compare/aaa...ccc"], Paths(), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_reviewer_on_the_head_has_zero_new_commits_and_no_compare_is_made()
    {
        _ = _server.AddReview(Repo, 12, "bob", "APPROVED", "ccc", _t);

        var read = (await Client().PullReviews(Repo, 12, "ccc", Ct)).Value;

        Assert.Equal([new ConnectorReview("bob", "ccc", 0)], read.Reviews);
        Assert.Equal(["/repos/acme/widgets/pulls/12/reviews"], Paths(), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_compare_answering_404_stores_the_count_as_unknown()
    {
        _ = _server.AddReview(Repo, 12, "bob", "APPROVED", "gone", _t);

        var read = (await Client().PullReviews(Repo, 12, "ccc", Ct)).Value;

        Assert.Equal([new ConnectorReview("bob", "gone", NewCommits: null)], read.Reviews);
        Assert.Null(read.RateLimitedUntil);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_reviewers_on_the_same_commit_share_one_compare()
    {
        _ = _server.AddReview(Repo, 12, "ada", "COMMENTED", "aaa", _t).AddReview(Repo, 12, "bob", "APPROVED", "aaa", _t.AddHours(1)).AddCompare(Repo, "aaa", "ccc", 3);

        var read = (await Client().PullReviews(Repo, 12, "ccc", Ct)).Value;

        Assert.Equal([new ConnectorReview("ada", "aaa", 3), new ConnectorReview("bob", "aaa", 3)], read.Reviews);
        _ = Assert.Single(Paths(), static p => p.Contains("/compare/", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rate_limit_on_the_compare_stops_the_read_with_the_reset_time()
    {
        var reset = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        _ = _server.AddReview(Repo, 12, "bob", "APPROVED", "aaa", _t).ScriptThrough().ScriptRateLimit(reset);

        var read = (await Client().PullReviews(Repo, 12, "ccc", Ct)).Value;

        Assert.Equal(reset, read.RateLimitedUntil);
        Assert.Empty(read.Reviews);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rate_limit_on_the_review_list_stops_the_read_with_the_reset_time()
    {
        var reset = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        _ = _server.AddReview(Repo, 12, "bob", "APPROVED", "aaa", _t).ScriptRateLimit(reset);

        var read = (await Client().PullReviews(Repo, 12, "ccc", Ct)).Value;

        Assert.Equal(reset, read.RateLimitedUntil);
        Assert.Equal(["/repos/acme/widgets/pulls/12/reviews"], Paths(), StringComparer.Ordinal);
    }

    private sealed class PublicResolver : IHostResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> Resolve(string host, CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Parse("140.82.112.5")]);
    }
}
