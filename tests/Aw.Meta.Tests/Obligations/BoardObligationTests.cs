using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Obligations;

/// <summary>The lanes read the board through their prompt hook (aw-meta board-line), so the meta no longer owes them a lane map or a main announcement.</summary>
public sealed class BoardObligationTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_lane_map_nobody_was_sent_opens_no_broadcast()
    {
        var lanes = new LaneMapState(3, [A.Assigned("meta", "t1-meta"), A.Assigned("api", "t1-api"), A.Assigned("engine", "t1-engine")]);
        var facts = A.Facts() with { Lanes = lanes };

        Assert.DoesNotContain(ObligationEvaluator.Evaluate(facts), o => o.Kind == ObligationKind.BroadcastLanes);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_merge_nobody_announced_opens_no_announcement()
    {
        var lanes = new LaneMapState(0, [A.Assigned("meta", "t1-meta"), A.Assigned("api", "t1-api")]);
        var facts = A.Facts() with { Lanes = lanes, LatestMerge = new MergeRecord(190, "abcdef1234567890", A.At(10)) };

        Assert.DoesNotContain(ObligationEvaluator.Evaluate(facts), o => o.Kind == ObligationKind.AnnounceMain);
    }
}
