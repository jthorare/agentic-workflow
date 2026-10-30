using Aw.Context.Core.Surfaces;

namespace Aw.Context.Tests.Surfaces;

/// <summary>A group's key and the cut of a big set of lookalikes (spec 6.9 "One item", "Which candidates are asked about").</summary>
[Trait("Lane", "unit")]
public sealed class SurfaceGroupTests
{
    [Fact]
    public void The_key_is_sha256_hex_and_the_same_for_the_same_candidates_on_a_rerun()
    {
        var first = SurfaceTestData.Group("Grant access", "Share access");
        var again = SurfaceTestData.Group("Grant access", "Share access");

        Assert.Matches("^[0-9a-f]{64}$", first.Key);
        Assert.Equal(first.Key, again.Key);
    }

    [Fact]
    public void The_key_bytes_are_pinned_node_key_kind_pass_then_sorted_raw_evidence_with_no_edges_or_names()
    {
        var group = new SurfaceGroup([
            new SurfaceCandidate("action:b", "action", "Whatever", "actions", ["z.ts"], ["writes thing:x"]),
            new SurfaceCandidate("action:a", "action", "Other", "actions", ["y.ts", "x.ts"], []),
        ]);

        // sha256 of "action:a\naction\nactions\nx.ts\ny.ts\naction:b\naction\nactions\nz.ts\n"
        Assert.Equal("20000714e4dca8867cb7557fe07910a264ce22916cc2b89a431f015e69d4a6e1", group.Key);
    }

    [Fact]
    public void The_key_does_not_depend_on_the_order_the_candidates_or_their_evidence_are_listed_in()
    {
        var a = new SurfaceGroup([SurfaceTestData.Candidate("A", "action:a", "x.ts", "y.ts"), SurfaceTestData.Candidate("B", "action:b", "z.ts")]);
        var b = new SurfaceGroup([SurfaceTestData.Candidate("B", "action:b", "z.ts"), SurfaceTestData.Candidate("A", "action:a", "y.ts", "x.ts")]);

        Assert.Equal(a.Key, b.Key);
    }

    [Fact]
    public void The_key_changes_when_the_evidence_or_a_node_key_changes()
    {
        var before = new SurfaceGroup([SurfaceTestData.Candidate("A", "action:a", "x.ts")]);
        var moreEvidence = new SurfaceGroup([SurfaceTestData.Candidate("A", "action:a", "x.ts", "y.ts")]);
        var otherKey = new SurfaceGroup([SurfaceTestData.Candidate("A", "action:b", "x.ts")]);

        Assert.NotEqual(before.Key, moreEvidence.Key, StringComparer.Ordinal);
        Assert.NotEqual(before.Key, otherKey.Key, StringComparer.Ordinal);
    }

    [Fact]
    public void Seven_lookalikes_are_cut_in_key_order_into_three_three_and_one()
    {
        string[] keys = ["g", "c", "a", "f", "b", "e", "d"];
        var lookalikes = keys.Select(k => SurfaceTestData.Candidate("Grant " + k, "action:" + k));

        var groups = SurfaceGroups.Cut(lookalikes);

        Assert.Equal([3, 3, 1], groups.Select(g => g.Candidates.Count));
        Assert.Equal(["action:a", "action:b", "action:c", "action:d", "action:e", "action:f", "action:g"], groups.SelectMany(g => g.Candidates).Select(c => c.NodeKey), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData(1, new[] { 1 })]
    [InlineData(3, new[] { 3 })]
    [InlineData(4, new[] { 3, 1 })]
    [InlineData(6, new[] { 3, 3 })]
    public void Any_set_is_cut_into_groups_of_at_most_three(int count, int[] sizes) =>
        Assert.Equal(sizes, SurfaceGroups.Cut(Enumerable.Range(0, count).Select(i => SurfaceTestData.Candidate("N" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), "action:" + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)))).Select(g => g.Candidates.Count));

    [Fact]
    public void Nothing_to_cut_makes_no_group() => Assert.Empty(SurfaceGroups.Cut([]));

    [Fact]
    public void The_status_line_names_why_nothing_was_decided_and_is_absent_when_jev_was_asked()
    {
        var verdicts = new[] { SurfaceVerdict.Separate("k", decided: false) };
        var counts = new SurfaceCounts(0, 1, 0, 1);

        Assert.Equal("product surfaces: not decided (off)", new SurfacePickResult(verdicts, counts, "off").StatusLine);
        Assert.Equal("product surfaces: not decided (no-key)", new SurfacePickResult(verdicts, counts, "no-key").StatusLine);
        Assert.Null(new SurfacePickResult(verdicts, counts, NotDecided: null).StatusLine);
    }
}
