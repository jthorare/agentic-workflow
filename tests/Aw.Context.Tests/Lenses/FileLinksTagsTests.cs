using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Lenses;

/// <summary>Spec 2026-10-27 §2.3 "Who reads Implements": a file has one home, and its tags are suggestions listed after it.</summary>
public sealed class FileLinksTagsTests
{
    private static GraphSnapshot Graph() => new GraphBuilder()
        .Node("feature:work.billing", provenance: Provenance.Deterministic).Node("feature:tag:work:auth", provenance: Provenance.Model, confidence: 0.7)
        .Node("feature:tag:work:audit", provenance: Provenance.Model, confidence: 0.6)
        .Edge("code:src/billing/a.ts", EdgeType.Tags, "feature:tag:work:auth", provenance: Provenance.Model, confidence: 0.7)
        .Edge("code:src/billing/a.ts", EdgeType.Implements, "feature:work.billing", provenance: Provenance.Deterministic)
        .Edge("code:src/billing/a.ts", EdgeType.Tags, "feature:tag:work:audit", provenance: Provenance.Model, confidence: 0.6)
        .Edge("code:src/loose/b.ts", EdgeType.Tags, "feature:tag:work:auth", provenance: Provenance.Model, confidence: 0.7)
        .Build();

    [Fact, Trait("Lane", "unit")]
    public void A_file_lists_its_home_first_and_its_tags_after_it_marked_as_tags()
    {
        var features = FileLinks.For(Graph()).FeaturesOf("src/billing/a.ts", confirmedOnly: false).ToList();

        Assert.Equal(["feature:work.billing", "feature:tag:work:audit", "feature:tag:work:auth"], features.Select(f => f.Key.Value), StringComparer.Ordinal);
        Assert.Equal([EdgeType.Implements, EdgeType.Tags, EdgeType.Tags], features.Select(f => f.Edge));
        Assert.True(features[0].Confirmed);
        Assert.All(features.Skip(1), f => Assert.False(f.Confirmed));
        Assert.All(features.Skip(1), f => Assert.NotNull(f.LinkId));
    }

    [Fact, Trait("Lane", "unit")]
    public void Tags_are_the_suggestions_and_the_home_is_never_one()
    {
        var suggestions = FileLinks.For(Graph()).Suggestions("src/billing/a.ts").ToList();

        Assert.Equal(["feature:tag:work:audit", "feature:tag:work:auth"], suggestions.Select(s => s.Feature.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_with_a_home_is_mapped_and_a_tag_only_file_is_suggested()
    {
        var links = FileLinks.For(Graph());

        Assert.Equal(FileMapStatus.Mapped, links.StatusOf("src/billing/a.ts"));
        Assert.Equal(FileMapStatus.Suggested, links.StatusOf("src/loose/b.ts"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tag_only_file_has_no_confirmed_feature() =>
        Assert.Empty(FileLinks.For(Graph()).FeaturesOf("src/loose/b.ts", confirmedOnly: true));
}
