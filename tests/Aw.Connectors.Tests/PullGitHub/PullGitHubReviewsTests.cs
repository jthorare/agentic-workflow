using System.Net;
using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Features;
using Aw.Connectors.Features.PullGitHub;
using Aw.Connectors.Infrastructure.GitHub;
using Aw.Connectors.Testing;
using Aw.Connectors.Testing.GitHub;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Connectors.Tests.PullGitHub;

/// <summary>PR review page spec §5.6 and D21 over the real client and a fake GitHub server: the facts a pull request record carries, and when the review reads are made.</summary>
public sealed class PullGitHubReviewsTests : IDisposable
{
    private const string Repo = "acme/widgets";
    private static readonly DateTimeOffset _t = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeGitHubServer _server = new();

    public void Dispose() => _server.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IService<PullGitHubRequest, PullGitHubResponse> Service()
    {
        var client = new GitHubClient(new HttpClient(_server), new GitHubClientSettings(), new PublicResolver(), new FakeTimeProvider(_t));
        var services = new ServiceCollection();
        _ = services.AddSingleton<IGitHubReader>(client).AddConnectorsFeatures();
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IService<PullGitHubRequest, PullGitHubResponse>>();
    }

    private int Calls(string part) => _server.Requests.Count(r => r.Uri.AbsolutePath.Contains(part, StringComparison.Ordinal));

    private static PullGitHubRequest Request(GitHubCursor? after = null) => A.PullGitHubRequest().After(after ?? GitHubCursor.From(_t.AddDays(-1))).Build();

    [Fact, Trait("Lane", "integration")]
    public async Task A_pull_request_with_two_requested_reviewers_carries_both_and_its_head()
    {
        _ = _server.Add(Repo, A.GitHubIssue(12).AsPullRequest().UpdatedAt(_t))
            .AddPull(Repo, 12, new GitHubPullDetail(Merged: false, Draft: false, MergedAt: null, [], ["bob", "cy"], "head1"));

        var item = Assert.Single((await Service().Handle(Request(), Ct)).Value.Items);

        var pull = Assert.IsType<ConnectorPullFacts>(item.Pull);
        Assert.Equal(["bob", "cy"], pull.RequestedReviewers);
        Assert.Equal("head1", pull.HeadSha);
        Assert.Empty(pull.Reviews);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_pull_request_asked_of_a_team_alone_stores_no_reviewers()
    {
        // The fake server always lists a requested team beside the people.
        _ = _server.Add(Repo, A.GitHubIssue(12).AsPullRequest().UpdatedAt(_t))
            .AddPull(Repo, 12, new GitHubPullDetail(Merged: false, Draft: false, MergedAt: null, [], [], "head1"));

        var item = Assert.Single((await Service().Handle(Request(), Ct)).Value.Items);

        Assert.Empty(item.Pull!.RequestedReviewers);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_pull_request_reviewed_at_one_commit_and_pushed_to_keeps_that_commit_and_how_many_commits_it_is_behind()
    {
        _ = _server.Add(Repo, A.GitHubIssue(12).AsPullRequest().UpdatedAt(_t))
            .AddPull(Repo, 12, new GitHubPullDetail(Merged: false, Draft: false, MergedAt: null, [], [], "commitC"))
            .AddReview(Repo, 12, "bob", "APPROVED", "commitA", _t.AddHours(-2))
            .AddCompare(Repo, "commitA", "commitC", 2);

        var item = Assert.Single((await Service().Handle(Request(), Ct)).Value.Items);

        Assert.Equal([new ConnectorReview("bob", "commitA", 2)], item.Pull!.Reviews);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_second_tick_with_nothing_changed_reads_no_reviews_and_makes_no_compare()
    {
        _ = _server.Add(Repo, A.GitHubIssue(12).AsPullRequest().UpdatedAt(_t))
            .AddPull(Repo, 12, new GitHubPullDetail(Merged: false, Draft: false, MergedAt: null, [], [], "commitC"))
            .AddReview(Repo, 12, "bob", "APPROVED", "commitA", _t.AddHours(-2))
            .AddCompare(Repo, "commitA", "commitC", 2);
        var service = Service();
        var first = (await service.Handle(Request(), Ct)).Value;
        var (reviews, compares) = (Calls("/reviews"), Calls("/compare/"));

        var second = (await service.Handle(Request(first.Next), Ct)).Value;

        Assert.Empty(second.Items);
        Assert.Equal((1, 1), (reviews, compares));
        Assert.Equal((reviews, compares), (Calls("/reviews"), Calls("/compare/")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_closed_pull_request_makes_no_reviews_request()
    {
        _ = _server.Add(Repo, A.GitHubIssue(12).AsPullRequest().Closed().UpdatedAt(_t))
            .AddPull(Repo, 12, new GitHubPullDetail(Merged: false, Draft: false, MergedAt: null, [], [], "commitC"))
            .AddReview(Repo, 12, "bob", "APPROVED", "commitA", _t.AddHours(-2));

        var item = Assert.Single((await Service().Handle(Request(), Ct)).Value.Items);

        Assert.Equal(0, Calls("/reviews"));
        Assert.Empty(item.Pull!.Reviews);
        Assert.Equal("commitC", item.Pull.HeadSha);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_issue_carries_no_pull_facts()
    {
        _ = _server.Add(Repo, A.GitHubIssue(7).UpdatedAt(_t));

        var item = Assert.Single((await Service().Handle(Request(), Ct)).Value.Items);

        Assert.Null(item.Pull);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_rate_limit_in_the_review_read_cuts_the_page_before_that_pull_request()
    {
        var reset = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        _ = _server.Add(Repo, A.GitHubIssue(12).AsPullRequest().UpdatedAt(_t))
            .AddPull(Repo, 12, new GitHubPullDetail(Merged: false, Draft: false, MergedAt: null, [], [], "commitC"))
            .ScriptThrough().ScriptThrough().ScriptThrough().ScriptRateLimit(reset);

        var response = (await Service().Handle(Request(), Ct)).Value;

        Assert.Equal(reset, response.RateLimitedUntil);
        Assert.Empty(response.Items);
    }

    private sealed class PublicResolver : IHostResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> Resolve(string host, CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Parse("140.82.112.5")]);
    }
}
