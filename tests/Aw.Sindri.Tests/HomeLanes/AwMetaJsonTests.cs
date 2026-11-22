using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Home;

namespace Aw.Sindri.Tests.HomeLanes;

/// <summary>The aw-meta JSON reader on output captured from a real 11-lane box (2026-11-21, <see cref="LaneFixtures"/>), and on a lane whose size aw-meta can't tell.</summary>
[Lane(TestLane.Unit)]
public sealed class AwMetaJsonTests
{
    [Fact]
    public void The_real_capture_reads_lanes_queue_and_a_limited_subject()
    {
        var read = AwMetaJson.Parse(LaneFixtures.ContextRecycle, LaneFixtures.Status, LaneFixtures.UsageLimited, teamList: null);

        Assert.Null(read.Unavailable);
        Assert.Equal(200_000, read.RecycleAt);
        Assert.Equal(["meta:Ok:146236", "api:Recycle:212004"], read.Lanes.Select(static l => $"{l.Lane}:{l.Verdict}:{l.Tokens}").ToArray());
        Assert.All(read.Lanes, static l => Assert.Null(l.SessionName));   // the real status has no lanes.entries: no session name, so no lane-to-PR edge
        Assert.Equal([2897, 2950, 3214], read.MergeQueue.Select(static q => q.Pr).ToArray());
        Assert.Equal("queued 2", read.MergeQueue[2].Github);
        var engine = Assert.Single(read.Usage, static u => u.State == "Limited");
        Assert.Equal(("engine", "Session", new DateTimeOffset(2026, 11, 21, 23, 0, 0, TimeSpan.Zero)), (engine.Subject, engine.Kind, engine.ResetsAt!.Value));
        Assert.Null(read.RepoDirectory);
    }

    [Fact]
    public void A_lane_of_unknown_size_keeps_the_read_and_reads_null_tokens()
    {
        const string context = """{"recycleAt":200000,"lanes":[{"lane":"codex","threshold":200000,"verdict":"Unknown","reason":"not a Claude Code lane"}],"attention":false}""";

        var read = AwMetaJson.Parse(context, LaneFixtures.StatusEmpty, LaneFixtures.UsageOk, teamList: null);

        Assert.Null(Assert.Single(read.Lanes).Tokens);
        Assert.Empty(read.MergeQueue);
    }
}
