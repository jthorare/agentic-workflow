using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Vectors;
using Aw.Context.Testing.Vectors;
using Aw.Kernel;

namespace Aw.Context.Tests.ConceptSynthesis;

/// <summary>The grouper over hand-made 2-D vectors, scanned exactly by the fake dot-product scan.</summary>
public sealed class ConceptGrouperTests
{
    private static readonly FakeVectorScan _scan = new();
    private static readonly float[] _threeDim = [1f, 0f, 0f];

    // A unit vector at the given angle in degrees.
    private static float[] At(double degrees) => [(float)Math.Cos(degrees * Math.PI / 180), (float)Math.Sin(degrees * Math.PI / 180)];

    private static RecordVector Rec(string key, string source, double degrees, string version = "v1") => new(key, version, source, At(degrees));

    [Fact, Trait("Lane", "unit")]
    public void Records_from_different_sources_that_are_close_form_one_group()
    {
        var outcome = ConceptGrouper.Group([Rec("linear:A-1", "linear", 0), Rec("github:pr/2", "github", 5), Rec("slack:t/3", "slack", 10)], new GroupingSettings(), _scan);
        Assert.True(outcome.IsSuccess);
        var result = outcome.Value;

        var group = Assert.Single(result.Groups);
        Assert.Equal(3, group.SourceCount);
        Assert.Equal(["github:pr/2", "linear:A-1", "slack:t/3"], group.Members.Select(m => m.Record), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Five_records_from_one_source_still_form_a_group()
    {
        var records = Enumerable.Range(0, 5).Select(i => Rec(string.Create(CultureInfo.InvariantCulture, $"docs:{i}"), "docs", i * 3)).ToList();

        var group = Assert.Single(ConceptGrouper.Group(records, new GroupingSettings(), _scan).Value.Groups);

        Assert.Equal(5, group.Members.Count);
        Assert.Equal(1, group.SourceCount);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lone_record_forms_no_group()
    {
        var outcome = ConceptGrouper.Group([Rec("docs:a", "docs", 0), Rec("docs:b", "docs", 90)], new GroupingSettings(), _scan);
        Assert.True(outcome.IsSuccess);
        var result = outcome.Value;

        Assert.Empty(result.Groups);
    }

    [Fact, Trait("Lane", "unit")]
    public void Edges_below_the_threshold_do_not_join_groups()
    {
        // 0/5 and 60/65 are two tight pairs; 5 to 60 is 55 degrees, cosine 0.57.
        var records = new[] { Rec("a", "x", 0), Rec("b", "x", 5), Rec("c", "x", 60), Rec("d", "x", 65) };

        var outcome = ConceptGrouper.Group(records, new GroupingSettings(), _scan);
        Assert.True(outcome.IsSuccess);
        var result = outcome.Value;

        Assert.Equal(2, result.Groups.Count);
        Assert.All(result.Groups, g => Assert.Equal(2, g.Members.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_component_over_the_cap_is_split_at_a_tighter_threshold()
    {
        // Two tight triples 30 degrees apart (cosine 0.866) joined at the default threshold, split once it passes 0.866.
        var records = new[] { Rec("a", "x", 0), Rec("b", "x", 5), Rec("c", "x", 10), Rec("d", "x", 40), Rec("e", "x", 45), Rec("f", "x", 50) };

        var outcome = ConceptGrouper.Group(records, new GroupingSettings(Neighbours: 5, MaxGroup: 3), _scan);
        Assert.True(outcome.IsSuccess);
        var result = outcome.Value;

        Assert.Equal(2, result.Groups.Count);
        Assert.Equal(["a", "b", "c"], result.Groups[0].Members.Select(m => m.Record), StringComparer.Ordinal);
        Assert.Equal(["d", "e", "f"], result.Groups[1].Members.Select(m => m.Record), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_membership_hash_changes_when_a_record_joins_or_a_version_moves_and_not_otherwise()
    {
        var settings = new GroupingSettings();
        var baseline = ConceptGrouper.Group([Rec("a", "x", 0), Rec("b", "x", 5)], settings, _scan).Value.Groups[0].MembershipHash;

        var again = ConceptGrouper.Group([Rec("b", "x", 6), Rec("a", "x", 1)], settings, _scan).Value.Groups[0].MembershipHash;
        var joined = ConceptGrouper.Group([Rec("a", "x", 0), Rec("b", "x", 5), Rec("c", "x", 8)], settings, _scan).Value.Groups[0].MembershipHash;
        var moved = ConceptGrouper.Group([Rec("a", "x", 0, "v2"), Rec("b", "x", 5)], settings, _scan).Value.Groups[0].MembershipHash;

        Assert.Equal(baseline, again, StringComparer.Ordinal);
        Assert.NotEqual(baseline, joined, StringComparer.Ordinal);
        Assert.NotEqual(baseline, moved, StringComparer.Ordinal);
        Assert.Matches("^[0-9a-f]{64}$", baseline);
    }

    [Fact, Trait("Lane", "unit")]
    public void Edge_cosines_include_edges_below_the_threshold()
    {
        var outcome = ConceptGrouper.Group([Rec("a", "x", 0), Rec("b", "x", 5), Rec("c", "x", 90)], new GroupingSettings(), _scan);
        Assert.True(outcome.IsSuccess);
        var result = outcome.Value;

        Assert.Equal(3, result.EdgeCosines.Count);
        Assert.Contains(result.EdgeCosines, c => c < 0.1f);
        _ = Assert.Single(result.Groups);
    }

    [Fact, Trait("Lane", "unit")]
    public void Mean_normalizes_and_skips_records_without_chunks()
    {
        var mean = RecordVectors.Mean("r", "v1", "docs", [[2f, 0f], [0f, 2f]]);

        Assert.NotNull(mean);
        Assert.Equal(1f, MathF.Sqrt(mean.Vector.ToArray().Sum(v => v * v)), 4);
        Assert.Equal(mean.Vector.Span[0], mean.Vector.Span[1], 4);
        Assert.Null(RecordVectors.Mean("r", "v1", "docs", []));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_scan_error_is_returned_not_thrown()
    {
        var outcome = ConceptGrouper.Group([Rec("a", "x", 0), Rec("b", "x", 5)], new GroupingSettings(), new FailingScan());

        Assert.True(outcome.IsError);
        Assert.Equal(ContextErrors.VectorScanInvalid.Code, outcome.Error.Code, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Vectors_of_different_dimensions_are_an_error()
    {
        var outcome = ConceptGrouper.Group([Rec("a", "x", 0), new RecordVector("b", "v1", "x", _threeDim)], new GroupingSettings(), _scan);

        Assert.True(outcome.IsError);
    }

    private sealed class FailingScan : IVectorScan
    {
        public Result<IReadOnlyList<VectorHit>> Top(VectorMatrix matrix, ReadOnlyMemory<float> query, ReadOnlyMemory<bool> mask, int count) =>
            ContextErrors.VectorScanInvalid.With("test failure");
    }
}
