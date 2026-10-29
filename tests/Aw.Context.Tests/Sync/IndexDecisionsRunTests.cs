using Aw.Context.Core.Records;
using Aw.Context.Features.SyncSources;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Infrastructure;
using Microsoft.Extensions.Time.Testing;
using MapWorld = Aw.Context.Tests.Map.MapWorld;

namespace Aw.Context.Tests.Sync;

/// <summary>When the index-decisions run last ended and the code it ended with are kept in the record store, so the status and a restarted process read them (#1597 part A, piece 4).</summary>
public sealed class IndexDecisionsRunTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IndexSync Rig(IRecordStore store, FakeTimeProvider clock) =>
        new(new RecordFeedSync(new FakeRecordFeedCatalog(new FakeRecordFeed(RecordSamples.Linear, [])), store, clock), store, new FakeScrubberFactory());

    [Fact, Trait("Lane", "unit")]
    public async Task The_concept_status_has_no_last_run_until_the_decision_pass_ran_and_then_its_end()
    {
        var store = new FakeRecordStore();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
        var index = Rig(store, clock);
        var before = (await index.ConceptStatus(MapWorld.Human, Ct)).Value;

        _ = await index.Decide(MapWorld.Human, Ct);
        var after = (await index.ConceptStatus(MapWorld.Human, Ct)).Value;

        Assert.Equal((null, null), (before.LastRun, before.LastCode));
        Assert.Equal((clock.GetUtcNow(), null), (after.LastRun, after.LastCode));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_last_run_survives_a_restart()
    {
        using var home = new TempHome();
        _ = await Rig(new SqliteRecordStore(home.Database), home.Clock).Decide(MapWorld.Human, Ct);

        var restarted = (await Rig(new SqliteRecordStore(home.Database), home.Clock).ConceptStatus(MapWorld.Human, Ct)).Value;

        Assert.Equal(home.Clock.GetUtcNow(), restarted.LastRun);
    }
}
