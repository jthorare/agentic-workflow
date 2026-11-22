using Aw.Kernel.Testing;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Home;

/// <summary>The lane-fed rows (home spec §3.2, §5.1): lane-recycle on verdict Recycle, agent-limited on state Limited, one pr-ready-merge per merge-queue entry.</summary>
[Lane(TestLane.Unit)]
public sealed class HomeLaneRowsTests
{
    private const string Repo = "jthorare/agentic-workflow";
    private static readonly DateTimeOffset _read = new(2026, 11, 12, 9, 10, 0, TimeSpan.Zero);

    // the #3082 box state: meta and ideas at the mark, ui under it, two PRs in the queue (3099 from meta's session), one subject limited
    private static MetaTeamRead Box(params MetaQueuedPr[] queue) => new(
        200_000,
        [
            new MetaLane("meta", "sindri-v3-meta", "6f1c2a9e", 205_431, 200_000, "Recycle"),
            new MetaLane("ideas", "sindri-v3-ideas", Session: null, 210_000, 200_000, "Recycle"),
            new MetaLane("ui", "sindri-v3-ui", "aa", 120_000, 200_000, "Ok"),
            new MetaLane("codex-lane", SessionName: null, Session: null, Tokens: null, Threshold: 200_000, Verdict: "Unknown"),
        ],
        queue.Length == 0 ? [new MetaQueuedPr(3099, "sindri-v3-meta", "Pass", Github: null), new MetaQueuedPr(3101, "sindri-v3-ui", "none", Github: null)] : queue,
        [new MetaUsage("qa", "Limited", "Weekly", new DateTimeOffset(2026, 11, 14, 17, 0, 0, TimeSpan.Zero), "claude-code"), new MetaUsage("meta", "Ok", Kind: null, ResetsAt: null, "claude-code")],
        "/home/owner/repos/agentic-workflow",
        Unavailable: null);

    [Fact]
    public void The_box_state_gives_two_recycle_rows_one_limited_row_and_two_queue_rows_in_that_order()
    {
        var rows = HomeLaneRows.From(Box(), Repo, _read, new PassThroughScrubber());

        Assert.Equal(["lane:recycle:meta", "lane:recycle:ideas", "lane:limited:qa", $"gh:ready:{Repo}#3099", $"gh:ready:{Repo}#3101"], rows.Select(static r => r.Row.Id.Value).ToArray());
        Assert.Equal([HomeKind.LaneRecycle, HomeKind.LaneRecycle, HomeKind.AgentLimited, HomeKind.PrReadyMerge, HomeKind.PrReadyMerge], rows.Select(static r => r.Row.Kind).ToArray());
        Assert.All(rows, static r => Assert.Equal((HomeLaneRows.SourceName, _read, HomeState.Open, HomeGroup.NeedsYou), (r.Row.Source.Name, r.Row.Source.ReadAt, r.Row.State, r.Row.Group)));
    }

    [Fact]
    public void A_recycle_row_names_the_lane_and_its_size_and_rounds_its_why_chip_to_10k()
    {
        var meta = HomeLaneRows.From(Box(), Repo, _read, new PassThroughScrubber())[0].Row;

        Assert.Equal("meta: at 205k, recycle from a digest", meta.Title);
        Assert.Equal(["at 200k", "meta", "lane meta"], meta.Facts.Select(static f => f.Text).ToArray());
        Assert.Equal(["copy-digest-request", "open-session"], meta.Actions.ToArray());
        Assert.Equal(HomeLaneRows.LaneWeight, meta.Rank.Weight);
        Assert.Equal(["lane", "session"], meta.Links.Select(static l => l.Kind).ToArray());
        Assert.Equal("at 210k", HomeLaneRows.RecycleWhy(219_999));
        Assert.Equal("at the mark", HomeLaneRows.RecycleWhy(null));
    }

    [Fact]
    public void A_recycle_row_blocks_the_queued_prs_its_lane_sent()
    {
        var rows = HomeLaneRows.From(Box(), Repo, _read, new PassThroughScrubber());

        var edge = Assert.Single(rows[0].Edges);
        Assert.Equal(("lane:recycle:meta", $"gh:ready:{Repo}#3099", HomeEdgeKind.ItemOf), (edge.From, edge.To, edge.Edge));
        Assert.Empty(rows[1].Edges);
    }

    [Fact]
    public void A_limited_row_says_which_limit_and_when_it_resets()
    {
        var qa = HomeLaneRows.From(Box(), Repo, _read, new PassThroughScrubber())[2].Row;

        Assert.Equal("qa: stopped at its weekly limit, resets Nov 14 17:00 UTC", qa.Title);
        Assert.Equal(["limited", "qa", "claude-code"], qa.Facts.Select(static f => f.Text).ToArray());
        Assert.Equal(["open-session"], qa.Actions.ToArray());
    }

    [Fact]
    public void A_queue_row_offers_merge_until_meta_is_merging_it()
    {
        var rows = HomeLaneRows.From(Box(new MetaQueuedPr(3099, "sindri-v3-meta", "Pass", Github: null), new MetaQueuedPr(3101, "sindri-v3-ui", "Pass", "queued 2 (AWAITING_CHECKS)")), Repo, _read, new PassThroughScrubber());

        var ready = rows[3].Row;
        var merging = rows[4].Row;
        Assert.Equal(("PR 3099 is ready to merge", "you"), (ready.Title, ready.WaitsOn));
        Assert.Equal(["merge", "open"], ready.Actions.ToArray());
        Assert.Equal(["ready", "#3099", "meta queue"], ready.Facts.Select(static f => f.Text).ToArray());
        Assert.Equal($"gh:pr:{Repo}#3099", Assert.Single(ready.Links).Id);
        Assert.Equal("meta is merging (queued 2 (AWAITING_CHECKS))", merging.WaitsOn);
        Assert.Equal(["open"], merging.Actions.ToArray());
        Assert.Equal(HomeLaneRows.ReadyWeight, ready.Rank.Weight);
    }

    [Fact]
    public void Without_the_repository_a_queue_row_is_keyed_by_number_and_has_no_pr_link()
    {
        var rows = HomeLaneRows.From(Box(), repo: null, _read, new PassThroughScrubber());

        Assert.Equal("lane:ready:3099", rows[3].Row.Id.Value);
        Assert.Empty(rows[3].Row.Links);
        Assert.Equal("lane:ready:3099", Assert.Single(rows[0].Edges).To);
    }

    [Fact]
    public void An_unavailable_read_and_a_name_that_cannot_be_an_id_make_no_rows()
    {
        var odd = Box() with { Lanes = [new MetaLane("two words", SessionName: null, Session: null, 300_000, 200_000, "Recycle")], MergeQueue = [new MetaQueuedPr(0, "x", "none", Github: null)], Usage = [] };

        Assert.Empty(HomeLaneRows.From(MetaTeamRead.NotAvailable("no aw-meta"), Repo, _read, new PassThroughScrubber()));
        Assert.Empty(HomeLaneRows.From(odd, Repo, _read, new PassThroughScrubber()));
    }
}
