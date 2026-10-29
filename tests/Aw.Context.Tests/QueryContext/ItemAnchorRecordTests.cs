using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;
using static Aw.Context.Tests.SyncSources.ExtractDecisionsTests;

namespace Aw.Context.Tests.QueryContext;

/// <summary>The item shape reads the record its key names ahead of the search, and tells a queued decision of it under the feed that stored the record (#1598).</summary>
[Trait("Lane", "integration")]
public sealed class ItemAnchorRecordTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ContextQuery Item(string key) => new(Text: null, Item: key);

    [Fact]
    public async Task The_anchor_record_is_cited_once_when_the_search_returns_it_too()
    {
        var leg = new FakeLexicalLeg().Add(new ChunkKey(RecordKey.From(R103), 0), AudienceKey.From("linear:SPL"));
        await using var rig = await Rig.Start(lexical: leg);
        _ = await rig.Sync(Scope());

        var result = (await rig.World.Engine.Query(Item(R103), MapWorld.Human, Ct)).Value;

        _ = Assert.Single(result.Cites.Values, c => c.Key.Value == R103);
    }

    [Fact]
    public async Task A_queued_decision_is_told_once_when_the_Linear_intent_source_is_among_the_sources()
    {
        var linear = new FakeContextSource(new SourceDescriptor(SourceId.From("linear"), SourceKind.Intent, SourceFreshness.Live, SupportsAsOf: false), []);
        await using var rig = await Rig.Start(intent: linear);
        _ = await rig.Sync(Scope(labeling: false));
        _ = await rig.Sync(Scope(labeling: false));

        var result = (await rig.World.Engine.Query(Item(R103), Scope(labeling: false), Ct)).Value;

        var held = Assert.Single(result.Degraded, d => d.Code == ContextErrors.LabelingUnavailable.Code);
        Assert.Equal("decisions", held.Source.Value);
    }
}
