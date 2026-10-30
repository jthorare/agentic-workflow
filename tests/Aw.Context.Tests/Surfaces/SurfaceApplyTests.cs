using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Surfaces;

namespace Aw.Context.Tests.Surfaces;

/// <summary>The override rows a surface verdict writes (spec 6.9, apply step).</summary>
[Trait("Lane", "unit")]
public sealed class SurfaceApplyTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);
    private static readonly SurfaceGroup _pair = new([
        SurfaceTestData.Candidate("Grant access", "action:a", "a.ts"),
        SurfaceTestData.Candidate("Share access", "action:b", "b.ts"),
        SurfaceTestData.Candidate("Give access", "action:c", "c.ts"),
    ]);

    [Fact]
    public void Reject_writes_one_reject_row_per_candidate_by_jev()
    {
        var rows = SurfaceApply.Rows([_pair], [SurfaceVerdict.Rejected(_pair.Key)], [], _now);

        Assert.Equal(["action:a", "action:b", "action:c"], rows.Select(r => r.Target.Value), StringComparer.Ordinal);
        Assert.All(rows, r =>
        {
            Assert.Equal(OverrideAction.Reject, r.Action);
            Assert.Equal(MapOverride.JevWho, r.Who);
            Assert.Equal(_now, r.At);
            Assert.Null(r.Other);
        });
    }

    [Fact]
    public void Merge_writes_one_merge_row_per_absorbed_candidate_into_the_survivor()
    {
        var rows = SurfaceApply.Rows([_pair], [SurfaceVerdict.Merged(_pair.Key, 1, "action:b", "Share access")], [], _now);

        Assert.Equal(["action:a", "action:c"], rows.Select(r => r.Target.Value), StringComparer.Ordinal);
        Assert.All(rows, r =>
        {
            Assert.Equal(OverrideAction.Merge, r.Action);
            Assert.Equal("action:b", r.Other?.Value);
            Assert.Equal(MapOverride.JevWho, r.Who);
        });
    }

    [Fact]
    public void Separate_writes_nothing()
    =>
        Assert.Empty(SurfaceApply.Rows([_pair], [SurfaceVerdict.Separate(_pair.Key, decided: true)], [], _now));

    [Fact]
    public void A_survivor_that_is_not_in_the_group_writes_nothing()
    =>
        Assert.Empty(SurfaceApply.Rows([_pair], [SurfaceVerdict.Merged(_pair.Key, 0, "action:gone", "Gone")], [], _now));

    [Fact]
    public void A_verdict_for_an_unknown_group_is_ignored()
    =>
        Assert.Empty(SurfaceApply.Rows([_pair], [SurfaceVerdict.Rejected("nope")], [], _now));

    [Fact]
    public void A_row_already_present_is_not_written_again()
    {
        MapOverride[] existing = [new(OverrideAction.Reject, NodeKey.From("action:a"), MapOverride.JevWho, _now.AddDays(-1))];

        var rows = SurfaceApply.Rows([_pair], [SurfaceVerdict.Rejected(_pair.Key)], existing, _now);

        Assert.Equal(["action:b", "action:c"], rows.Select(r => r.Target.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void A_row_already_present_but_undone_is_written_again()
    {
        var earlier = new MapOverride(OverrideAction.Reject, NodeKey.From("action:a"), MapOverride.JevWho, _now.AddDays(-2));
        var undo = new MapOverride(OverrideAction.Undo, NodeKey.From("action:a"), "joi", _now.AddDays(-1), Undoes: earlier.ToId());

        var rows = SurfaceApply.Rows([_pair], [SurfaceVerdict.Rejected(_pair.Key)], [earlier, undo], _now);

        Assert.Contains(rows, r => r.Target.Value == "action:a");
    }

    [Fact]
    public void A_persons_row_of_the_same_kind_does_not_count_as_present()
    {
        MapOverride[] existing = [new(OverrideAction.Reject, NodeKey.From("action:a"), "joi", _now.AddDays(-1))];

        Assert.Equal(3, SurfaceApply.Rows([_pair], [SurfaceVerdict.Rejected(_pair.Key)], existing, _now).Count);
    }
}
