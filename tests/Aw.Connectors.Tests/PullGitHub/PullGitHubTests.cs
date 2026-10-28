using System.Globalization;
using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Features;
using Aw.Connectors.Features.PullGitHub;
using Aw.Connectors.Testing;
using Aw.Connectors.Testing.GitHub;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Connectors.Tests.PullGitHub;

public sealed class PullGitHubTests
{
    private const string Repo = "acme/widgets";
    private static DateTimeOffset T => new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IService<PullGitHubRequest, PullGitHubResponse> Service(FakeGitHubReader reader)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IGitHubReader>(reader).AddConnectorsFeatures();
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IService<PullGitHubRequest, PullGitHubResponse>>();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_issue_becomes_a_record_with_its_key_permalink_and_cleaned_text()
    {
        var reader = new FakeGitHubReader().Add(Repo, A.GitHubIssue(7).UpdatedAt(T.AddHours(1)).WithBody("<!-- note -->Crash on **save**.\r\n\r\n![shot](x.png)"));

        var response = (await Service(reader).Handle(A.PullGitHubRequest().Build(), Ct)).Value;

        var item = Assert.Single(response.Items);
        Assert.Equal("github:issue/acme/widgets/7", item.Key);
        Assert.Equal("issue", item.Kind);
        Assert.Equal("Issue 7", item.Title);
        Assert.Equal("Issue 7\n\nCrash on save.", item.Text);
        Assert.Equal("https://github.com/acme/widgets/issues/7", item.Url);
        Assert.Equal("open", item.State);
        Assert.Equal("octocat", item.Author);
        Assert.Equal(Repo, item.Repo);
        Assert.Equal("2026-10-01T13:00:00.0000000Z", item.Version);
        Assert.Empty(item.Links);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_is_keyed_as_pr_and_links_the_issues_it_closes()
    {
        var reader = new FakeGitHubReader().Add(
            Repo,
            A.GitHubIssue(12).AsPullRequest().Closed().UpdatedAt(T.AddHours(1)).WithBody("Fixes #3 and closes #4. Also fixes #12 (itself) and fixes #3 again."));

        var item = Assert.Single((await Service(reader).Handle(A.PullGitHubRequest().Build(), Ct)).Value.Items);

        Assert.Equal("github:pr/acme/widgets/12", item.Key);
        Assert.Equal("pr", item.Kind);
        Assert.Equal("closed", item.State);
        Assert.True(item.Links.Select(static l => l.Key).SequenceEqual(["github:issue/acme/widgets/3", "github:issue/acme/widgets/4"], StringComparer.Ordinal));
        Assert.All(item.Links, static l => Assert.Equal("closes", l.Kind));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_issue_never_links_even_when_its_body_says_fixes()
    {
        var reader = new FakeGitHubReader().Add(Repo, A.GitHubIssue(5).UpdatedAt(T.AddHours(1)).WithBody("Fixes #3"));

        var item = Assert.Single((await Service(reader).Handle(A.PullGitHubRequest().Build(), Ct)).Value.Items);

        Assert.Empty(item.Links);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Comments_are_folded_into_the_text_oldest_first_and_a_hash_reference_relates()
    {
        var reader = new FakeGitHubReader()
            .Add(Repo, A.GitHubIssue(7).UpdatedAt(T.AddHours(3)).WithBody("Crash.").WithComments(2))
            .AddComment(Repo, A.GitHubComment(2).On(7).By("ada").UpdatedAt(T.AddHours(3)).WithBody("Same as #5, and see acme/other#9 and #7."))
            .AddComment(Repo, A.GitHubComment(1).On(7).By(author: null).UpdatedAt(T.AddHours(2)).WithBody("<!-- x -->Me too."))
            .AddComment(Repo, A.GitHubComment(3).On(8).UpdatedAt(T.AddHours(2)).WithBody("Other issue, mentions #6."));

        var item = Assert.Single((await Service(reader).Handle(A.PullGitHubRequest().Build(), Ct)).Value.Items);

        Assert.Equal("Issue 7\n\nCrash.\n\nghost: Me too.\n\nada: Same as #5, and see acme/other#9 and #7.", item.Text);
        Assert.Equal([new ConnectorLink("relates", "github:issue/acme/widgets/5")], item.Links);
        Assert.Equal(["comments acme/widgets"], reader.Reads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Only_the_newest_comments_up_to_the_cap_are_kept()
    {
        var reader = new FakeGitHubReader().Add(Repo, A.GitHubIssue(7).UpdatedAt(T.AddHours(5)).WithComments(3));
        for (var i = 1; i <= 3; i++)
        {
            _ = reader.AddComment(Repo, A.GitHubComment(i).On(7).UpdatedAt(T.AddHours(i)).WithBody(string.Create(CultureInfo.InvariantCulture, $"c{i}")));
        }

        var item = Assert.Single((await Service(reader).Handle(A.PullGitHubRequest().WithCommentCap(2).Build(), Ct)).Value.Items);

        Assert.EndsWith("hubot: c2\n\nhubot: c3", item.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("c1", item.Text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_zero_cap_reads_no_comments()
    {
        var reader = new FakeGitHubReader().Add(Repo, A.GitHubIssue(7).UpdatedAt(T.AddHours(1)).WithComments(2));

        _ = await Service(reader).Handle(A.PullGitHubRequest().WithCommentCap(0).Build(), Ct);

        Assert.Empty(reader.Reads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_issue_whose_older_comments_the_repo_wide_read_missed_is_read_on_its_own()
    {
        var reader = new FakeGitHubReader()
            .Add(Repo, A.GitHubIssue(7).UpdatedAt(T.AddHours(3)).WithComments(2))
            .AddComment(Repo, A.GitHubComment(1).On(7).UpdatedAt(T.AddDays(-30)).WithBody("old"))
            .AddComment(Repo, A.GitHubComment(2).On(7).UpdatedAt(T.AddHours(3)).WithBody("new"));

        var item = Assert.Single((await Service(reader).Handle(A.PullGitHubRequest().Build(), Ct)).Value.Items);

        Assert.EndsWith("hubot: old\n\nhubot: new", item.Text, StringComparison.Ordinal);
        Assert.Equal(["comments acme/widgets", "comments acme/widgets#7"], reader.Reads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rate_limit_on_the_repo_wide_comment_read_returns_nothing_and_leaves_the_cursor()
    {
        var reset = T.AddDays(3);
        var after = new GitHubCursor(T, [9]);
        var reader = new FakeGitHubReader().Add(Repo, A.GitHubIssue(7).UpdatedAt(T.AddHours(1)).WithComments(1)).RateLimitComments(reset);

        var response = (await Service(reader).Handle(A.PullGitHubRequest().After(after).Build(), Ct)).Value;

        Assert.Empty(response.Items);
        Assert.Equal(after, response.Next);
        Assert.Equal(reset, response.RateLimitedUntil);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merged_pull_request_is_merged_keeps_its_raw_state_and_touches_its_paths()
    {
        var reader = new FakeGitHubReader()
            .Add(Repo, A.GitHubIssue(12).AsPullRequest().Closed().UpdatedAt(T.AddHours(1)).WithBody("Fixes #3. Related to #4."))
            .AddPull(Repo, 12, new GitHubPullDetail(Merged: true, Draft: false, T.AddHours(1), ["src/a.cs", "src/a.cs", "../etc/passwd", "docs/b.md"]));

        var item = Assert.Single((await Service(reader).Handle(A.PullGitHubRequest().Build(), Ct)).Value.Items);

        Assert.Equal("merged", item.State);
        Assert.Equal("closed", item.StateRaw);
        Assert.Equal(
            [
                new ConnectorLink("closes", "github:issue/acme/widgets/3"),
                new ConnectorLink("relates", "github:issue/acme/widgets/4"),
                new ConnectorLink("touches", "path:acme/widgets/src/a.cs"),
                new ConnectorLink("touches", "path:acme/widgets/docs/b.md"),
            ],
            item.Links);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_open_draft_pull_request_is_a_draft_and_an_open_one_is_open()
    {
        var reader = new FakeGitHubReader()
            .Add(Repo, A.GitHubIssue(1).AsPullRequest().UpdatedAt(T.AddHours(1)))
            .Add(Repo, A.GitHubIssue(2).AsPullRequest().UpdatedAt(T.AddHours(2)))
            .AddPull(Repo, 1, new GitHubPullDetail(Merged: false, Draft: true, MergedAt: null, []));

        var items = (await Service(reader).Handle(A.PullGitHubRequest().Build(), Ct)).Value.Items;

        Assert.Equal(["draft", "open"], items.Select(static i => i.State), StringComparer.Ordinal);
        Assert.Equal(["open", "open"], items.Select(static i => i.StateRaw), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_issue_never_reads_a_pull_request_detail()
    {
        var reader = new FakeGitHubReader().Add(Repo, A.GitHubIssue(1).UpdatedAt(T.AddHours(1)));

        _ = await Service(reader).Handle(A.PullGitHubRequest().Build(), Ct);

        Assert.DoesNotContain(reader.Reads, static r => r.StartsWith("pull", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_next_cursor_and_the_rate_limit_come_through()
    {
        var reader = new FakeGitHubReader().Add(Repo, A.GitHubIssue(1).UpdatedAt(T.AddHours(2)));

        var response = (await Service(reader).Handle(A.PullGitHubRequest().Build(), Ct)).Value;

        Assert.Equal(T.AddHours(2), response.Next.UpdatedAt);
        Assert.Equal<long>([1], response.Next.SeenIds);
        Assert.Null(response.RateLimitedUntil);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_request_reaches_the_reader_unchanged()
    {
        var reader = new FakeGitHubReader();
        var after = new GitHubCursor(T, [9]);

        _ = await Service(reader).Handle(A.PullGitHubRequest().After(after).WithLimit(25).Build(), Ct);

        Assert.Equal((Repo, after, 25), Assert.Single(reader.Calls));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_reader_error_comes_back_as_the_error()
    {
        var reader = new FakeGitHubReader().Fail(ConnectorsErrors.Unauthorized.With("rejected"));

        var result = await Service(reader).Handle(A.PullGitHubRequest().Build(), Ct);

        Assert.True(result.IsError);
        Assert.Equal(ConnectorsErrors.Unauthorized.Code, result.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("widgets", 100)]
    [InlineData("acme/widgets/extra", 100)]
    [InlineData("acme/widgets", 0)]
    [InlineData("acme/widgets", 10001)]
    public async Task A_bad_repo_or_limit_is_refused_before_any_read(string repo, int limit)
    {
        var reader = new FakeGitHubReader();

        var result = await Service(reader).Handle(A.PullGitHubRequest().ForRepo(repo).WithLimit(limit).Build(), Ct);

        Assert.Equal(ConnectorsErrors.Invalid.Code, result.Error.Code);
        Assert.Empty(reader.Calls);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task A_comment_cap_outside_the_range_is_refused_before_any_read(int cap)
    {
        var reader = new FakeGitHubReader();

        var result = await Service(reader).Handle(A.PullGitHubRequest().WithCommentCap(cap).Build(), Ct);

        Assert.Equal(ConnectorsErrors.Invalid.Code, result.Error.Code);
        Assert.Empty(reader.Calls);
    }
}
