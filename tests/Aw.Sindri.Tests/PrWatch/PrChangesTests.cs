using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.PrWatch;

[Lane(TestLane.Unit)]
public sealed class PrChangesTests
{
    private const string Link = "https://forge.test/acme/widgets/pull/7";
    private static readonly DateTimeOffset _then = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    private static PrWatchEntry Seen(string digest, string head = "aaa", string state = "Open") => new(digest, head, state, _then);

    private static PullDigestRow Now(string digest, string head = "aaa", PullState state = PullState.Open) => new(Link, digest, state, head);

    [Fact]
    public void An_open_pull_request_seen_first_has_no_change()
    {
        Assert.Empty(PrChanges.Between(before: null, Now("t1|aaa|pending|0")));
    }

    [Theory]
    [InlineData(PullState.Merged, "merged")]
    [InlineData(PullState.Closed, "closed")]
    public void A_pull_request_seen_first_already_done_is_reported(PullState state, string expected)
    {
        Assert.Equal(new[] { expected }, PrChanges.Between(before: null, Now("t1|aaa|success|0", state: state)));
    }

    [Fact]
    public void An_equal_digest_state_and_head_is_no_change()
    {
        Assert.Empty(PrChanges.Between(Seen("t1|aaa|pending|0"), Now("t1|aaa|pending|0")));
    }

    [Fact]
    public void A_moved_head_is_updated()
    {
        Assert.Equal(new[] { "updated" }, PrChanges.Between(Seen("t1|aaa|pending|0"), Now("t2|bbb|pending|0", head: "bbb")));
    }

    [Fact]
    public void A_new_comment_is_a_review_change()
    {
        Assert.Equal(new[] { "review" }, PrChanges.Between(Seen("t1|aaa|success|0"), Now("t2|aaa|success|1")));
    }

    [Fact]
    public void A_check_changing_state_is_a_ci_change()
    {
        Assert.Equal(new[] { "ci" }, PrChanges.Between(Seen("t1|aaa|pending|0"), Now("t1|aaa|failure|0")));
    }

    [Fact]
    public void Several_changes_are_listed_in_the_tables_order()
    {
        Assert.Equal(new[] { "merged", "updated", "review", "ci" }, PrChanges.Between(Seen("t1|aaa|pending|0"), Now("t2|bbb|success|2", head: "bbb", state: PullState.Merged)));
    }

    [Fact]
    public void A_digest_that_moved_with_nothing_named_is_a_review_change()
    {
        Assert.Equal(new[] { "review" }, PrChanges.Between(Seen("t1|aaa|success|0"), Now("t2|aaa|success|0")));
        Assert.Equal(new[] { "review" }, PrChanges.Between(Seen("opaque-1"), Now("opaque-2")));
    }
}
