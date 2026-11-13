using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Connections;

/// <summary>
/// The GitHub kinds the events feed can't carry (#3201, #2988): github.pr.checks and github.pr.conflict come from the open pull requests' state
/// (GET /repos/{r}/pulls, then /commits/{head}/check-runs or /pulls/{n}), compared with the state the cursor holds. The first poll is a baseline;
/// a change fires once, and the same state on the next poll fires nothing. The new feed kinds (github.pr.updated ...) poll through the feed.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class GitHubPullStatePollTests
{
    private const string Repo = "acme/app";
    private const string Before = "2026-11-08T09:00:00Z";

    private static readonly EventKind _checks = new("github.pr.checks");
    private static readonly EventKind _conflict = new("github.pr.conflict");
    private static readonly EventKind _updated = new("github.pr.updated");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Checks_take_a_baseline_then_a_red_on_the_same_head_fires_once_with_the_failing_names()
    {
        var provider = new FakeProvider();
        provider.SetGitHubOpenPulls(GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:00:00Z"));
        provider.SetGitHubCheckRuns("abc123", GitHubFeedFixtures.CheckRuns(("build", "completed", "success", "2026-11-08T10:01:00Z"), ("lint", "in_progress", null, null)));
        using var h = PollerHarness.For("github", provider);

        var baseline = await h.Poller.Poll(h.Connection, _checks, cursor: null, Token);
        provider.SetGitHubCheckRuns("abc123", GitHubFeedFixtures.CheckRuns(("build", "completed", "success", "2026-11-08T10:01:00Z"), ("lint", "completed", "failure", "2026-11-08T10:02:00Z")));
        var red = await h.Poller.Poll(h.Connection, _checks, baseline.Value.NextCursor, Token);
        var again = await h.Poller.Poll(h.Connection, _checks, red.Value.NextCursor, Token);

        Assert.True(baseline.IsSuccess);
        Assert.Empty(baseline.Value.Events);
        Assert.NotNull(baseline.Value.NextCursor);
        var fired = Assert.Single(red.Value.Events);
        Assert.Equal(_checks, fired.Kind);
        Assert.Equal("red", fired.Fields["conclusion"]);
        Assert.Equal("[\"lint\"]", fired.Fields["failing"]);
        Assert.Equal("42", fired.Fields["number"]);
        Assert.Equal("github:acme/app#42:checks:abc123:red:7002", fired.DeliveryKey);
        Assert.Equal(DateTimeOffset.Parse("2026-11-08T10:02:00Z", System.Globalization.CultureInfo.InvariantCulture), fired.OccurredAt);   // the last check that finished
        Assert.Empty(again.Value.Events);   // still red on the same head: nothing new
    }

    [Fact]
    public async Task Checks_that_finish_green_fire_green_with_no_failing_names()
    {
        var provider = new FakeProvider();
        provider.SetGitHubOpenPulls(GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:00:00Z"));
        provider.SetGitHubCheckRuns("abc123", GitHubFeedFixtures.CheckRuns(("build", "queued", null, null)));
        using var h = PollerHarness.For("github", provider);

        var baseline = await h.Poller.Poll(h.Connection, _checks, cursor: null, Token);
        provider.SetGitHubCheckRuns("abc123", GitHubFeedFixtures.CheckRuns(("build", "completed", "success", "2026-11-08T10:05:00Z"), ("docs", "completed", "skipped", "2026-11-08T10:04:00Z")));
        var green = await h.Poller.Poll(h.Connection, _checks, baseline.Value.NextCursor, Token);

        var fired = Assert.Single(green.Value.Events);
        Assert.Equal("green", fired.Fields["conclusion"]);
        Assert.Equal("[]", fired.Fields["failing"]);
    }

    [Fact]
    public async Task A_new_head_that_goes_red_again_fires_again_under_its_own_delivery_id()
    {
        var provider = new FakeProvider();
        provider.SetGitHubOpenPulls(GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:00:00Z"));
        provider.SetGitHubCheckRuns("abc123", GitHubFeedFixtures.CheckRuns(("lint", "completed", "failure", "2026-11-08T10:02:00Z")));
        using var h = PollerHarness.For("github", provider);

        var baseline = await h.Poller.Poll(h.Connection, _checks, cursor: null, Token);
        provider.SetGitHubOpenPulls(GitHubFeedFixtures.OpenPull(Repo, 42, "def456", "2026-11-08T10:10:00Z"));
        provider.SetGitHubCheckRuns("def456", GitHubFeedFixtures.CheckRuns(("lint", "completed", "failure", "2026-11-08T10:12:00Z")));
        var red = await h.Poller.Poll(h.Connection, _checks, baseline.Value.NextCursor, Token);

        Assert.Equal("github:acme/app#42:checks:def456:red:7001", Assert.Single(red.Value.Events).DeliveryKey);
    }

    [Fact]
    public async Task A_pull_request_that_goes_dirty_fires_one_conflict()
    {
        var provider = new FakeProvider();
        provider.SetGitHubOpenPulls(GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:00:00Z"));
        provider.SetGitHubPull(42, GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:00:00Z", mergeableState: "clean"));
        using var h = PollerHarness.For("github", provider);

        var baseline = await h.Poller.Poll(h.Connection, _conflict, cursor: null, Token);
        provider.SetGitHubPull(42, GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:20:00Z", mergeableState: "dirty"));
        var dirty = await h.Poller.Poll(h.Connection, _conflict, baseline.Value.NextCursor, Token);
        var again = await h.Poller.Poll(h.Connection, _conflict, dirty.Value.NextCursor, Token);

        Assert.Empty(baseline.Value.Events);
        var fired = Assert.Single(dirty.Value.Events);
        Assert.Equal(_conflict, fired.Kind);
        Assert.Equal("github:acme/app#42:conflict:abc123:1", fired.DeliveryKey);
        Assert.Equal("main", fired.Fields["baseRef"]);
        Assert.Empty(again.Value.Events);
    }

    [Fact]
    public async Task A_mergeable_state_GitHub_has_not_worked_out_yet_fires_nothing_and_keeps_the_last_state()
    {
        var provider = new FakeProvider();
        provider.SetGitHubOpenPulls(GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:00:00Z"));
        provider.SetGitHubPull(42, GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:00:00Z", mergeableState: "clean"));
        using var h = PollerHarness.For("github", provider);

        var baseline = await h.Poller.Poll(h.Connection, _conflict, cursor: null, Token);
        provider.SetGitHubPull(42, GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:20:00Z", mergeableState: "unknown"));
        var unknown = await h.Poller.Poll(h.Connection, _conflict, baseline.Value.NextCursor, Token);
        provider.SetGitHubPull(42, GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:21:00Z", mergeableState: "dirty"));
        var dirty = await h.Poller.Poll(h.Connection, _conflict, unknown.Value.NextCursor, Token);

        Assert.Empty(unknown.Value.Events);
        _ = Assert.Single(dirty.Value.Events);
    }

    [Fact]
    public async Task A_new_feed_kind_polls_through_the_events_feed()
    {
        var provider = new FakeProvider();
        provider.AddGitHubEvent(GitHubFeedFixtures.PullRequest("5001", Repo, 42, "synchronize", "2026-11-08T10:00:00Z", headSha: "def456"));
        provider.AddGitHubEvent(GitHubFeedFixtures.PullRequest("5002", Repo, 43, "opened", "2026-11-08T10:01:00Z"));
        using var h = PollerHarness.For("github", provider);

        var polled = await h.Poller.Poll(h.Connection, _updated, Before, Token);

        var e = Assert.Single(polled.Value.Events);
        Assert.Equal("github:5001", e.DeliveryKey);
        Assert.Equal("def456", e.Fields["headSha"]);
    }

    [Fact]
    public async Task A_trimmed_event_gets_title_author_and_draft_from_one_pull_request_read()
    {
        var provider = new FakeProvider();
        provider.AddGitHubEvent(ProviderFixtures.GitHubPrTrimmed("6001", Repo, 77, "opened", "abc123", "2026-11-08T10:00:00Z"));
        provider.SetGitHubPull(77, GitHubFeedFixtures.OpenPull(Repo, 77, "abc123", "2026-11-08T10:00:00Z", mergeableState: "clean", draft: true));
        using var h = PollerHarness.For("github", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.GitHubPrOpened, Before, Token);

        var e = Assert.Single(polled.Value.Events);
        Assert.Equal(GitHubFeedFixtures.Title, e.Fields["title"]);
        Assert.Equal(GitHubFeedFixtures.Author, e.Fields["author"]);
        Assert.Equal("true", e.Fields["draft"]);
    }
}
