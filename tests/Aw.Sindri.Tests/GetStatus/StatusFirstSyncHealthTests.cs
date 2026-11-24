using Aw.Context.Core.Map;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ContextCalls;
using Aw.Sindri.Tests.Server;
using Aw.Sindri.Tests.Views;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>
/// A long first sync is normal work, not a health problem (#4035): through the real <see cref="ContextSourceHealth"/>, a source still on its first
/// pass that committed within the limit leaves the word healthy whatever serve's run time; one that committed nothing for the limit is degraded;
/// and a paused index-sync flags nothing.
/// </summary>
public sealed class StatusFirstSyncHealthTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly ViewsHarness _v = new();
    private readonly FakeIndexSync _index = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SliceHarness H => _v.Slice;

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_20_minutes_into_its_first_pass_that_committed_a_minute_ago_leaves_health_healthy()
    {
        var now = _start.AddMinutes(20);
        var backfilling = FirstPass(pull: "running", records: 12_400, lastCommit: now.AddMinutes(-1));

        var health = await Health(backfilling, TimeSpan.FromMinutes(20), new NoPauses());

        Assert.Equal(HealthWord.Healthy, health.Word);
        Assert.Empty(health.Problems);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_pass_with_no_new_record_for_15_minutes_is_degraded_with_one_problem()
    {
        var now = _start.AddMinutes(20);
        var stuck = FirstPass(pull: "idle", records: 12_400, lastCommit: now.AddMinutes(-15));

        var health = await Health(stuck, TimeSpan.FromMinutes(20), new NoPauses());

        Assert.Equal(HealthWord.Degraded, health.Word);
        Assert.Equal(new HealthProblem("SND-CONTEXT-010", "linear hasn't committed a record in 15 minutes", "the linear source needs a look"), Assert.Single(health.Problems));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_paused_index_sync_flags_no_source_through_the_real_source_health_4034()
    {
        var never = FirstPass(pull: "idle", records: 0, lastCommit: null) with { LastSync = null };

        var health = await Health(never, TimeSpan.FromMinutes(40), new PausedServices(IndexSyncService.ServiceId));

        Assert.Equal(HealthWord.Healthy, health.Word);
        Assert.Empty(health.Problems);
    }

    private async Task<HealthReport> Health(SourceProgress source, TimeSpan served, IServicePauses pauses)
    {
        H.Profiles.Live = SliceHarness.Profile("5s");
        _ = await H.HoldLease();
        _v.Publish(stalledAt: null);
        _index.Standing = [source];
        var clock = new FakeTimeProvider(_start);
        var started = new SourceSyncClock(clock);
        clock.Advance(served);
        var engine = IndexStoreEngineProxy.Create(_index, new MapHealth("context:store", MapHealthState.Ok, "ok", Fix: null));
        var sources = new ContextSourceHealth(new ContextScopes(H.Profiles, H.Callers, new FakeRepoCheckouts()), engine, started, pauses);
        var service = new GetStatusService(H.Held, new ProfileReads(H.Profiles, H.Profiles), H.Store, _v.Progress, new HealthServiceRows(_v.Views, [], sources: sources), new InMemorySessionSnapshot());

        var status = await service.Handle(new GetStatusRequest(), Token);

        return status.Value.Health!;
    }

    private static SourceProgress FirstPass(string pull, int records, DateTimeOffset? lastCommit) =>
        new(SourceId.From("linear"), SyncPhase.Backfill, lastCommit ?? _start.AddMinutes(-1), LastCode: null, Usable: true, Complete: false)
        {
            Pull = pull,
            Records = records,
            LastCommit = lastCommit,
        };
}
