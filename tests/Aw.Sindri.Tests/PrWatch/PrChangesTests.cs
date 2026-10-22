using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.PrWatch;

[Lane(TestLane.Unit)]
public sealed class PrChangesTests
{
    private const string Link = "https://forge.test/acme/widgets/pull/7";
    private static readonly DateTimeOffset _then = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    private static PrWatchEntry Seen(string version, string head = "aaa", string state = "Open", PullChecksState checks = PullChecksState.Pending, int comments = 0) =>
        new(version, head, state, checks.ToString(), comments, _then);

    private static PullDigestRow Now(string version, string head = "aaa", PullState state = PullState.Open, PullChecksState checks = PullChecksState.Pending, int comments = 0) =>
        new(Link, state, head, checks, comments, version);

    [Fact]
    public void An_open_pull_request_seen_first_has_no_change()
    {
        Assert.Empty(PrChanges.Between(before: null, Now("t1")));
    }

    [Theory]
    [InlineData(PullState.Merged, "merged")]
    [InlineData(PullState.Closed, "closed")]
    public void A_pull_request_seen_first_already_done_is_reported(PullState state, string expected)
    {
        Assert.Equal(new[] { expected }, PrChanges.Between(before: null, Now("t1", state: state)));
    }

    [Fact]
    public void An_equal_version_state_head_checks_and_comments_is_no_change()
    {
        Assert.Empty(PrChanges.Between(Seen("t1"), Now("t1")));
    }

    [Fact]
    public void A_moved_head_is_updated()
    {
        Assert.Equal(new[] { "updated" }, PrChanges.Between(Seen("t1"), Now("t2", head: "bbb")));
    }

    [Fact]
    public void A_new_comment_is_a_review_change()
    {
        Assert.Equal(new[] { "review" }, PrChanges.Between(Seen("t1", checks: PullChecksState.Passing), Now("t2", checks: PullChecksState.Passing, comments: 1)));
    }

    [Fact]
    public void A_check_changing_state_is_a_ci_change()
    {
        Assert.Equal(new[] { "ci" }, PrChanges.Between(Seen("t1"), Now("t1", checks: PullChecksState.Failing)));
    }

    [Fact]
    public void Several_changes_are_listed_in_the_tables_order()
    {
        Assert.Equal(new[] { "merged", "updated", "review", "ci" }, PrChanges.Between(Seen("t1"), Now("t2", head: "bbb", state: PullState.Merged, checks: PullChecksState.Passing, comments: 2)));
    }

    [Fact]
    public void A_version_that_moved_with_nothing_typed_changed_is_a_review_change()
    {
        Assert.Equal(new[] { "review" }, PrChanges.Between(Seen("opaque-1"), Now("opaque-2")));
    }

    [Fact]
    public void The_version_text_is_not_read_only_compared()
    {
        Assert.Empty(PrChanges.Between(Seen("a|b|c|d"), Now("a|b|c|d")));
        Assert.Equal(new[] { "review" }, PrChanges.Between(Seen("a|b|c|d"), Now("a|b|x|d")));
    }
}
