using System.Globalization;
using Aw.Context.Core.Library;

namespace Aw.Context.Tests.Library;

/// <summary>The <c language="text">Covers:</c> grammar of spec 2026-11-04 §4.4: one line of refs, backticks allowed, the key in any case, or <c language="text">none (reason)</c>.</summary>
[Trait("Lane", "unit")]
public sealed class PrCoversTests
{
    [Fact]
    public void A_line_of_refs_is_split_on_commas()
    {
        var covers = PrCovers.Parse("## Summary\nCovers: harbor-booking-tdd#Ph2, harbor-booking-tdd#REQ-E3\n\nMore.");

        Assert.Equal(["harbor-booking-tdd#Ph2", "harbor-booking-tdd#REQ-E3"], covers.Refs);
        Assert.False(covers.Declined);
        Assert.False(covers.Doubled);
    }

    [Fact]
    public void Backticks_and_a_lowercase_key_are_fine()
    {
        var covers = PrCovers.Parse("covers: `harbor#Ph1`, `doc:aw/slug/harbor#REQ-2`");

        Assert.Equal(["harbor#Ph1", "doc:aw/slug/harbor#REQ-2"], covers.Refs);
    }

    [Fact]
    public void None_with_a_reason_claims_nothing_and_says_so()
    {
        var covers = PrCovers.Parse("COVERS: none (a typo fix)");

        Assert.Empty(covers.Refs);
        Assert.True(covers.Declined);
    }

    [Fact]
    public void A_second_Covers_line_claims_nothing()
    {
        var covers = PrCovers.Parse("Covers: a#Ph1\nCovers: b#Ph2");

        Assert.Empty(covers.Refs);
        Assert.True(covers.Doubled);
    }

    [Fact]
    public void A_ref_listed_twice_is_kept_once_and_text_that_is_not_a_ref_is_dropped()
    {
        var covers = PrCovers.Parse("Covers: a#Ph1, a#Ph1, and then some words");

        Assert.Equal(["a#Ph1"], covers.Refs);
    }

    [Fact]
    public void A_Covers_line_inside_a_code_fence_is_not_a_claim()
    {
        var covers = PrCovers.Parse("```\nCovers: a#Ph1\n```");

        Assert.Equal(PrCovers.None, covers);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Summary only. Discovers: x#y")]
    public void A_body_without_the_line_claims_nothing(string? body) =>
        Assert.Equal(PrCovers.None, PrCovers.Parse(body));

    [Fact]
    public void More_than_the_cap_of_refs_keeps_the_first_ones_and_says_so()
    {
        var refs = Enumerable.Range(1, PrCovers.MaxRefs + 50).Select(static n => $"harbor#REQ-{n.ToString(CultureInfo.InvariantCulture)}").ToList();

        var covers = PrCovers.Parse("Covers: " + string.Join(", ", refs));

        Assert.Equal(refs.Take(PrCovers.MaxRefs), covers.Refs, StringComparer.Ordinal);
        Assert.True(covers.Capped);
        Assert.False(PrCovers.Parse("Covers: harbor#REQ-1").Capped);
    }
}
