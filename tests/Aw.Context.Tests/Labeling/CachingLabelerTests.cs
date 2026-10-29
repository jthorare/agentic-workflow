using Aw.Context.Core;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Labeling;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Testing.Extract;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Labeling;

[Trait("Lane", "unit")]
public sealed class CachingLabelerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeLabeler _inner = new();
    private readonly FakeLabelCache _cache = new();
    private readonly RedactionScrubberFactory _factory = new(new FakeTimeProvider());

    private static FileCluster Cluster(string id, params string[] files) => new(ClusterId.From(id), files, 0.7);

    private CachingLabeler Labeler() => new(_inner, _cache, _factory);

    [Fact]
    public async Task A_cluster_whose_files_were_labeled_gets_its_label_under_its_new_id_without_asking()
    {
        _ = await Labeler().Label([Cluster("h00000", "a/x.ts", "a/y.ts")], [], 1000, Ct);
        _inner.Shown.Clear();

        var pass = await Labeler().Label([Cluster("h00000", "new/z.ts"), Cluster("h00001", "a/x.ts", "a/y.ts")], [], 1000, Ct);

        Assert.Equal(["h00000"], _inner.Shown.Select(c => c.Id.Value), StringComparer.Ordinal);
        Assert.Equal("Labeled h00000", pass.Value.Labels.Single(l => string.Equals(l.Id.Value, "h00001", StringComparison.Ordinal)).Label);
    }

    [Fact]
    public async Task A_pass_of_only_hits_never_calls_the_inner_labeler()
    {
        var clusters = new[] { Cluster("h00000", "a/x.ts") };
        _ = await Labeler().Label(clusters, [], 1000, Ct);
        _inner.Failure = ContextErrors.LabelingUnavailable.With("the inner labeler was called");

        var pass = await Labeler().Label(clusters, [], 1000, Ct);

        Assert.True(pass.IsSuccess);
        Assert.True(pass.Value.Complete);
        _ = Assert.Single(pass.Value.Labels);
    }

    [Fact]
    public async Task A_cache_that_cannot_be_read_or_written_still_labels()
    {
        _cache.Failure = ContextErrors.LabelingUnavailable.With("unreadable");

        var pass = await Labeler().Label([Cluster("h00000", "a/x.ts")], [], 1000, Ct);

        Assert.Equal("Labeled h00000", Assert.Single(pass.Value.Labels).Label);
    }

    [Fact]
    public async Task An_inner_error_with_hits_in_hand_is_an_incomplete_pass()
    {
        _ = await Labeler().Label([Cluster("h00000", "a/x.ts")], [], 1000, Ct);
        _inner.Failure = ContextErrors.LabelingUnavailable.With("model down");

        var pass = await Labeler().Label([Cluster("h00000", "a/x.ts"), Cluster("h00001", "b/y.ts")], [], 1000, Ct);

        Assert.False(pass.Value.Complete);
        Assert.Equal(["h00000"], pass.Value.Labels.Select(l => l.Id.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void Paths_that_scrub_to_the_same_text_share_a_key()
    {
        var egress = _factory.For([]).Value.Egress;
        var a = Cluster("c1", "src/secrets/ghp_" + new string('a', 36) + "/x.ts");
        var b = Cluster("c2", "src/secrets/ghp_" + new string('b', 36) + "/x.ts");

        Assert.Equal(CachingLabeler.Key(a, egress), CachingLabeler.Key(b, egress));
    }
}
