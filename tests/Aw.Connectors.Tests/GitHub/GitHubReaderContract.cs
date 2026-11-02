using System.Globalization;
using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Testing;

namespace Aw.Connectors.Tests.GitHub;

/// <summary>What every <see cref="IGitHubReader"/> does, run against the fake and the real client so the fake stays honest.</summary>
public abstract class GitHubReaderContract
{
    protected const string Repo = "acme/widgets";

    protected static DateTimeOffset T => new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected IGitHubReader Reader(IReadOnlyList<GitHubIssue> issues) => Reader(issues, [], new Dictionary<int, GitHubPullDetail>());

    protected abstract IGitHubReader Reader(IReadOnlyList<GitHubIssue> issues, IReadOnlyList<GitHubComment> comments, IReadOnlyDictionary<int, GitHubPullDetail> pulls);

    [Fact, Trait("Lane", "unit")]
    public async Task Entries_come_oldest_change_first_and_the_cursor_moves_to_the_newest()
    {
        var reader = Reader([A.GitHubIssue(2).UpdatedAt(T.AddMinutes(5)), A.GitHubIssue(1).UpdatedAt(T.AddMinutes(1))]);

        var page = (await reader.Issues(Repo, GitHubCursor.From(T), 100, before: null, Ct)).Value;

        Assert.Equal([1, 2], page.Items.Select(static i => i.Id));
        Assert.Equal(T.AddMinutes(5), page.Next.UpdatedAt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Entries_changed_at_or_after_before_are_left_out_and_the_cursor_stops_at_the_last_one_kept()
    {
        var reader = Reader([A.GitHubIssue(1).UpdatedAt(T.AddMinutes(1)), A.GitHubIssue(2).UpdatedAt(T.AddMinutes(5)), A.GitHubIssue(3).UpdatedAt(T.AddMinutes(9))]);

        var page = (await reader.Issues(Repo, GitHubCursor.From(T), 100, T.AddMinutes(5), Ct)).Value;

        Assert.Equal([1], page.Items.Select(static i => i.Id));
        Assert.Equal(T.AddMinutes(1), page.Next.UpdatedAt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Entries_the_cursor_has_seen_are_skipped()
    {
        var reader = Reader([A.GitHubIssue(1).UpdatedAt(T), A.GitHubIssue(2).UpdatedAt(T)]);

        var page = (await reader.Issues(Repo, new GitHubCursor(T, [1]), 100, before: null, Ct)).Value;

        Assert.Equal([2], page.Items.Select(static i => i.Id));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pull_requests_are_marked()
    {
        var reader = Reader([A.GitHubIssue(1).UpdatedAt(T.AddMinutes(1)).AsPullRequest()]);

        var page = (await reader.Issues(Repo, GitHubCursor.From(T), 100, before: null, Ct)).Value;

        Assert.True(Assert.Single(page.Items).IsPullRequest);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Repo_wide_comments_come_oldest_change_first_from_since_to_until_with_their_issue_number()
    {
        var reader = Reader(
            [],
            [
                A.GitHubComment(3).On(7).UpdatedAt(T.AddMinutes(30)).By("ada").WithBody("late"),
                A.GitHubComment(1).On(7).UpdatedAt(T.AddMinutes(10)).WithBody("first"),
                A.GitHubComment(2).On(9).UpdatedAt(T.AddMinutes(20)),
                A.GitHubComment(4).On(9).UpdatedAt(T.AddMinutes(-5)),
                A.GitHubComment(5).On(9).UpdatedAt(T.AddHours(5)),
            ],
            new Dictionary<int, GitHubPullDetail>());

        var page = (await reader.Comments(Repo, T, T.AddHours(1), Ct)).Value;

        Assert.Equal([1, 2, 3], page.Items.Select(static c => c.Id));
        Assert.Equal([7, 9, 7], page.Items.Select(static c => c.IssueNumber));
        Assert.Equal("ada", page.Items[2].Author);
        Assert.Equal("late", page.Items[2].Body);
        Assert.Equal(T.AddMinutes(30), page.Items[2].UpdatedAt);
        Assert.Null(page.RateLimitedUntil);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task One_issues_comments_come_oldest_first_and_only_its_own()
    {
        var reader = Reader(
            [],
            [A.GitHubComment(2).On(7).UpdatedAt(T.AddHours(3)), A.GitHubComment(1).On(7).UpdatedAt(T.AddHours(1)), A.GitHubComment(3).On(8).UpdatedAt(T.AddHours(2))],
            new Dictionary<int, GitHubPullDetail>());

        var page = (await reader.IssueComments(Repo, 7, Ct)).Value;

        Assert.Equal([1, 2], page.Items.Select(static c => c.Id));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_requests_merge_state_and_paths_are_read()
    {
        var merged = T.AddHours(4);
        var reader = Reader(
            [A.GitHubIssue(12).UpdatedAt(T.AddHours(1)).AsPullRequest()],
            [],
            new Dictionary<int, GitHubPullDetail> { [12] = new(Merged: true, Draft: false, merged, ["src/a.cs", "docs/b.md"]) });

        var read = (await reader.PullRequest(Repo, 12, Ct)).Value;

        var detail = Assert.IsType<GitHubPullDetail>(read.Detail);
        Assert.True(detail.Merged);
        Assert.False(detail.Draft);
        Assert.Equal(merged, detail.MergedAt);
        Assert.Equal(["src/a.cs", "docs/b.md"], detail.Paths);
        Assert.Null(read.RateLimitedUntil);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_with_more_paths_than_three_pages_is_cut_at_three_hundred()
    {
        var paths = Enumerable.Range(0, 350).Select(static i => string.Create(CultureInfo.InvariantCulture, $"src/f{i}.cs")).ToList();
        var reader = Reader(
            [A.GitHubIssue(12).UpdatedAt(T.AddHours(1)).AsPullRequest()],
            [],
            new Dictionary<int, GitHubPullDetail> { [12] = new(Merged: false, Draft: true, MergedAt: null, paths) });

        var detail = (await reader.PullRequest(Repo, 12, Ct)).Value.Detail!;

        Assert.True(detail.Draft);
        Assert.Equal(paths.Take(300), detail.Paths, StringComparer.Ordinal);
    }
}
