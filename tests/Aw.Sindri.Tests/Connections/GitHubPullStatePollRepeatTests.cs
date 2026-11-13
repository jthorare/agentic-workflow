using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Connections;

/// <summary>
/// What the pull request state poll must not get wrong (#3201, review of #3554): a pull request that ranks back into the 20 the poll reads is not a change,
/// a state that repeats on one head is still a new event, a push that leaves a pull request dirty fires nothing, and a pull request opened since the
/// last poll is the one case where a first sighting fires.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class GitHubPullStatePollRepeatTests
{
    private const string Repo = "acme/app";
    private const string Old = "2025-12-01T00:00:00Z";   // before the harness clock (KnownInstants.Epoch, 2026-01-01)
    private const string New = "2026-01-01T00:05:00Z";    // after it

    private static readonly EventKind _checks = new("github.pr.checks");
    private static readonly EventKind _conflict = new("github.pr.conflict");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Sha(int number) => "sha" + number.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public async Task A_stale_red_pull_request_past_the_cap_that_ranks_into_the_top_20_fires_nothing()
    {
        var provider = new FakeProvider();
        var pulls = Enumerable.Range(1, 30).Select(n => GitHubFeedFixtures.OpenPull(Repo, n, Sha(n), "2026-11-08T10:00:00Z", createdAt: Old)).ToList();
        foreach (var n in Enumerable.Range(1, 30))
        {
            provider.SetGitHubCheckRuns(Sha(n), GitHubFeedFixtures.CheckRuns(("build", "completed", n == 25 ? "failure" : "success", "2026-10-01T10:00:00Z")));
        }

        provider.SetGitHubOpenPulls([.. pulls]);
        using var h = PollerHarness.For("github", provider);

        var baseline = await h.Poller.Poll(h.Connection, _checks, cursor: null, Token);
        provider.SetGitHubOpenPulls([pulls[24], .. pulls.Where((_, i) => i != 24)]);   // a comment moves #25 to the front
        var next = await h.Poller.Poll(h.Connection, _checks, baseline.Value.NextCursor, Token);

        Assert.Empty(baseline.Value.Events);
        Assert.True(next.IsSuccess);
        Assert.Empty(next.Value.Events);
    }

    [Fact]
    public async Task A_walked_pull_request_that_drops_past_the_cap_and_comes_back_unchanged_fires_nothing()
    {
        var provider = new FakeProvider();
        var pulls = Enumerable.Range(1, 30).Select(n => GitHubFeedFixtures.OpenPull(Repo, n, Sha(n), "2026-11-08T10:00:00Z", createdAt: Old)).ToList();
        foreach (var n in Enumerable.Range(1, 30))
        {
            provider.SetGitHubCheckRuns(Sha(n), GitHubFeedFixtures.CheckRuns(("build", "completed", "success", "2026-10-01T10:00:00Z")));
        }

        provider.SetGitHubOpenPulls([.. pulls]);
        using var h = PollerHarness.For("github", provider);

        var first = await h.Poller.Poll(h.Connection, _checks, cursor: null, Token);   // #5 is walked and stored
        provider.SetGitHubOpenPulls([.. pulls.Where((_, i) => i != 4), pulls[4]]);   // #5 drops to rank 30: not walked, entry carried
        var second = await h.Poller.Poll(h.Connection, _checks, first.Value.NextCursor, Token);
        provider.SetGitHubOpenPulls([.. pulls]);
        var third = await h.Poller.Poll(h.Connection, _checks, second.Value.NextCursor, Token);

        Assert.Empty(second.Value.Events);
        Assert.Empty(third.Value.Events);
    }

    [Fact]
    public async Task Red_then_green_then_red_on_one_head_is_two_red_events_with_different_delivery_ids()
    {
        var provider = new FakeProvider();
        provider.SetGitHubOpenPulls(GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:00:00Z", createdAt: Old));
        provider.SetGitHubCheckRuns("abc123", GitHubFeedFixtures.CheckRuns(("lint", "completed", "success", "2026-11-08T10:01:00Z")));
        using var h = PollerHarness.For("github", provider);

        var baseline = await h.Poller.Poll(h.Connection, _checks, cursor: null, Token);
        provider.SetGitHubCheckRuns("abc123", GitHubFeedFixtures.CheckRunsFrom(7002, ("lint", "completed", "failure", "2026-11-08T10:02:00Z")));
        var red1 = await h.Poller.Poll(h.Connection, _checks, baseline.Value.NextCursor, Token);
        provider.SetGitHubCheckRuns("abc123", GitHubFeedFixtures.CheckRunsFrom(7003, ("lint", "completed", "success", "2026-11-08T10:05:00Z")));
        var green = await h.Poller.Poll(h.Connection, _checks, red1.Value.NextCursor, Token);
        provider.SetGitHubCheckRuns("abc123", GitHubFeedFixtures.CheckRunsFrom(7004, ("lint", "completed", "failure", "2026-11-08T10:09:00Z")));
        var red2 = await h.Poller.Poll(h.Connection, _checks, green.Value.NextCursor, Token);

        var first = Assert.Single(red1.Value.Events);
        var second = Assert.Single(red2.Value.Events);
        Assert.Equal("red", first.Fields["conclusion"]);
        Assert.Equal("red", second.Fields["conclusion"]);
        Assert.NotEqual(first.DeliveryKey, second.DeliveryKey, StringComparer.Ordinal);   // the poll round drops a delivery id it has already started a run for
    }

    [Fact]
    public async Task Dirty_then_clean_then_dirty_is_two_conflict_events_with_different_delivery_ids()
    {
        var provider = new FakeProvider();
        provider.SetGitHubOpenPulls(GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:00:00Z", createdAt: Old));
        provider.SetGitHubPull(42, GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:00:00Z", mergeableState: "clean"));
        using var h = PollerHarness.For("github", provider);

        var baseline = await h.Poller.Poll(h.Connection, _conflict, cursor: null, Token);
        provider.SetGitHubPull(42, GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:10:00Z", mergeableState: "dirty"));
        var dirty1 = await h.Poller.Poll(h.Connection, _conflict, baseline.Value.NextCursor, Token);
        provider.SetGitHubPull(42, GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:20:00Z", mergeableState: "clean"));
        var clean = await h.Poller.Poll(h.Connection, _conflict, dirty1.Value.NextCursor, Token);
        provider.SetGitHubPull(42, GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:30:00Z", mergeableState: "dirty"));
        var dirty2 = await h.Poller.Poll(h.Connection, _conflict, clean.Value.NextCursor, Token);

        var first = Assert.Single(dirty1.Value.Events);
        var second = Assert.Single(dirty2.Value.Events);
        Assert.NotEqual(first.DeliveryKey, second.DeliveryKey, StringComparer.Ordinal);
        Assert.Empty(clean.Value.Events);
    }

    [Fact]
    public async Task A_new_pull_request_whose_mergeable_state_is_unknown_on_first_sight_fires_when_it_turns_dirty()
    {
        var provider = new FakeProvider();
        provider.SetGitHubOpenPulls(GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:00:00Z", createdAt: Old));   // opened after the cursor's "@at" below
        provider.SetGitHubPull(42, GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:00:00Z", mergeableState: "unknown"));
        using var h = PollerHarness.For("github", provider);

        var unknown = await h.Poller.Poll(h.Connection, _conflict, "{\"@at\":\"2025-11-01T00:00:00Z\"}", Token);
        provider.SetGitHubPull(42, GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:10:00Z", mergeableState: "dirty"));
        var dirty = await h.Poller.Poll(h.Connection, _conflict, unknown.Value.NextCursor, Token);   // its "@at" is now past the pull request's creation

        Assert.Empty(unknown.Value.Events);
        Assert.Equal("42", Assert.Single(dirty.Value.Events).Fields["number"]);
    }

    [Fact]
    public async Task An_old_pull_request_whose_mergeable_state_is_unknown_on_the_first_poll_fires_nothing_when_it_reads_dirty()
    {
        var provider = new FakeProvider();
        provider.SetGitHubOpenPulls(GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:00:00Z", createdAt: Old));
        provider.SetGitHubPull(42, GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:00:00Z", mergeableState: "unknown"));
        using var h = PollerHarness.For("github", provider);

        var baseline = await h.Poller.Poll(h.Connection, _conflict, cursor: null, Token);   // the connection's first poll: GitHub has not worked the state out
        provider.SetGitHubPull(42, GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:10:00Z", mergeableState: "dirty"));
        var dirty = await h.Poller.Poll(h.Connection, _conflict, baseline.Value.NextCursor, Token);   // a conflict that predates the connection

        Assert.Empty(baseline.Value.Events);
        Assert.Empty(dirty.Value.Events);
    }

    [Fact]
    public async Task A_dirty_pull_request_pushed_to_a_new_head_that_stays_dirty_fires_nothing()
    {
        var provider = new FakeProvider();
        provider.SetGitHubOpenPulls(GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:00:00Z", createdAt: Old));
        provider.SetGitHubPull(42, GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:00:00Z", mergeableState: "clean"));
        using var h = PollerHarness.For("github", provider);

        var baseline = await h.Poller.Poll(h.Connection, _conflict, cursor: null, Token);
        provider.SetGitHubPull(42, GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", "2026-11-08T10:10:00Z", mergeableState: "dirty"));
        var dirty = await h.Poller.Poll(h.Connection, _conflict, baseline.Value.NextCursor, Token);
        provider.SetGitHubPull(42, GitHubFeedFixtures.OpenPull(Repo, 42, "def456", "2026-11-08T10:20:00Z", mergeableState: "dirty"));
        var pushed = await h.Poller.Poll(h.Connection, _conflict, dirty.Value.NextCursor, Token);

        _ = Assert.Single(dirty.Value.Events);
        Assert.Empty(pushed.Value.Events);
    }

    [Fact]
    public async Task A_pull_request_opened_since_the_last_poll_fires_on_first_sight_and_an_old_one_first_seen_does_not()
    {
        var provider = new FakeProvider();
        provider.SetGitHubOpenPulls(GitHubFeedFixtures.OpenPull(Repo, 1, "sha1", "2026-11-08T10:00:00Z", createdAt: Old));
        provider.SetGitHubCheckRuns("sha1", GitHubFeedFixtures.CheckRuns(("build", "completed", "success", "2026-11-08T10:00:00Z")));
        using var h = PollerHarness.For("github", provider);

        var baseline = await h.Poller.Poll(h.Connection, _checks, cursor: null, Token);
        provider.SetGitHubOpenPulls(
            GitHubFeedFixtures.OpenPull(Repo, 2, "sha2", "2026-11-08T11:00:00Z", createdAt: New),   // opened after the baseline poll, already red
            GitHubFeedFixtures.OpenPull(Repo, 3, "sha3", "2026-11-08T10:30:00Z", createdAt: Old),   // old, first walked now, red: not a change
            GitHubFeedFixtures.OpenPull(Repo, 1, "sha1", "2026-11-08T10:00:00Z", createdAt: Old));
        provider.SetGitHubCheckRuns("sha2", GitHubFeedFixtures.CheckRuns(("build", "completed", "failure", "2026-11-08T11:00:00Z")));
        provider.SetGitHubCheckRuns("sha3", GitHubFeedFixtures.CheckRuns(("build", "completed", "failure", "2026-11-08T10:30:00Z")));
        var next = await h.Poller.Poll(h.Connection, _checks, baseline.Value.NextCursor, Token);

        Assert.Equal("2", Assert.Single(next.Value.Events).Fields["number"]);
    }

    [Fact]
    public async Task The_at_key_is_kept_by_every_poll_and_is_never_read_as_a_pull_request_or_dropped_as_closed()
    {
        var provider = new FakeProvider();
        provider.SetGitHubOpenPulls(
            GitHubFeedFixtures.OpenPull(Repo, 1, "sha1", "2026-11-08T10:00:00Z", createdAt: Old),
            GitHubFeedFixtures.OpenPull(Repo, 2, "sha2", "2026-11-08T10:00:00Z", createdAt: Old));
        provider.SetGitHubCheckRuns("sha1", GitHubFeedFixtures.CheckRuns(("build", "completed", "success", "2026-11-08T10:00:00Z")));
        provider.SetGitHubCheckRuns("sha2", GitHubFeedFixtures.CheckRuns(("build", "completed", "success", "2026-11-08T10:00:00Z")));
        using var h = PollerHarness.For("github", provider);

        var first = await h.Poller.Poll(h.Connection, _checks, cursor: null, Token);
        provider.SetGitHubOpenPulls(GitHubFeedFixtures.OpenPull(Repo, 1, "sha1", "2026-11-08T10:00:00Z", createdAt: Old));   // #2 closed
        var second = await h.Poller.Poll(h.Connection, _checks, first.Value.NextCursor, Token);
        var third = await h.Poller.Poll(h.Connection, _checks, second.Value.NextCursor, Token);

        using var cursor = System.Text.Json.JsonDocument.Parse(third.Value.NextCursor!);
        Assert.Equal(["1", "@at"], cursor.RootElement.EnumerateObject().Select(static p => p.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Empty(second.Value.Events);
        Assert.Empty(third.Value.Events);
    }

    [Fact]
    public async Task A_cursor_without_the_at_key_marks_no_pull_request_as_new_so_an_unknown_one_fires_nothing()
    {
        var provider = new FakeProvider();
        provider.SetGitHubOpenPulls(
            GitHubFeedFixtures.OpenPull(Repo, 43, "sha43", "2026-11-08T11:00:00Z", createdAt: New),   // looks new, but there is no "@at" to say so
            GitHubFeedFixtures.OpenPull(Repo, 42, "abc", "2026-11-08T10:00:00Z", createdAt: Old));
        provider.SetGitHubCheckRuns("sha43", GitHubFeedFixtures.CheckRuns(("build", "completed", "failure", "2026-11-08T11:00:00Z")));
        provider.SetGitHubCheckRuns("abc", GitHubFeedFixtures.CheckRuns(("build", "completed", "failure", "2026-11-08T10:00:00Z")));
        using var h = PollerHarness.For("github", provider);

        var polled = await h.Poller.Poll(h.Connection, _checks, "{\"42\":\"abc:red\"}", Token);

        Assert.True(polled.IsSuccess);
        Assert.Empty(polled.Value.Events);
        Assert.Contains("\"@at\"", polled.Value.NextCursor, StringComparison.Ordinal);   // the next poll has one
    }
}
