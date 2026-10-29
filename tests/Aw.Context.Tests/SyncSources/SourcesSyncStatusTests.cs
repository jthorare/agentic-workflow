using Aw.Context.Core;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Features.SyncSources;
using static Aw.Context.Tests.SyncSources.ExtractDecisionsTests;

namespace Aw.Context.Tests.SyncSources;

/// <summary>'context sources status' for an intent source (S-5B, spec §8.8): when it last synced, and what the sync holds.</summary>
[Trait("Lane", "integration")]
public sealed class SourcesSyncStatusTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<SourceStatusRow> Linear(Rig rig) =>
        (await rig.World.Engine.Sources(Scope(), Ct)).Value.Sources.Single(r => r.Source.Value == "linear");

    [Fact]
    public async Task SourcesStatus_shows_last_sync_links_decisions_queued_per_intent_source()
    {
        await using var rig = await Rig.Start();
        var before = (await Linear(rig)).Sync;

        _ = await rig.Sync(Scope());
        var after = (await Linear(rig)).Sync;

        Assert.Equal((null, SourceState.Degraded, 0), (before?.LastSync, before?.State, before?.Records));   // never synced
        Assert.NotNull(after);
        var layer = await rig.Layer();
        Assert.Equal((SourceState.Ok, layer.Records.Count, layer.Links.Count, layer.Decisions.Count, 0, null),
            (after.State, after.Records, after.Linked, after.Decisions, after.Queued, after.Code));
        Assert.True(after.LastSync is not null);
        Assert.True(after.Decisions > 0);
    }

    [Fact]
    public async Task While_labeling_is_off_and_records_are_queued_status_shows_042_after_every_run_and_the_source_stays_ok()
    {
        await using var rig = await Rig.Start();
        _ = await rig.Sync(Scope(labeling: false));
        var replay = await rig.Sync(Scope(labeling: false));   // fetches nothing, so its own event carries no 042

        var sync = (await Linear(rig)).Sync;

        Assert.True(replay.Queued > 0);
        Assert.NotNull(sync);
        Assert.Equal((SourceState.Ok, ContextErrors.LabelingUnavailable.Code), (sync.State, sync.Code));
        Assert.True(sync.Queued > 0);
    }

    [Fact]
    public async Task Status_drops_042_once_the_queue_is_extracted()
    {
        await using var rig = await Rig.Start();
        _ = await rig.Sync(Scope(labeling: false));
        _ = await rig.Sync(Scope());

        var sync = (await Linear(rig)).Sync;

        Assert.Equal((0, null), (sync?.Queued, sync?.Code));
    }

    [Fact]
    public async Task A_run_cut_short_by_the_time_cap_shows_059_and_stays_ok_and_042_stays_for_labeling()
    {
        await using var rig = await Rig.Start();
        rig.World.Model.Fail = _ =>
        {
            rig.World.Clock.Advance(SyncSourcesService.RunCap + TimeSpan.FromSeconds(1));   // the first model call takes longer than the whole run may
            return null;
        };

        var capped = await rig.Sync(Scope());
        var cut = (await Linear(rig)).Sync;
        rig.World.Model.Fail = null;
        _ = await rig.Sync(Scope());
        var after = (await Linear(rig)).Sync;

        Assert.Equal(ContextErrors.SyncCapped.Code, capped.Code);
        Assert.Equal((SourceState.Ok, ContextErrors.SyncCapped.Code), (cut?.State, cut?.Code));   // partial, not failed: the next run continues
        Assert.Equal((SourceState.Ok, null), (after?.State, after?.Code));
    }

    [Fact]
    public async Task A_model_failure_in_a_run_the_time_cap_also_stopped_shows_the_failure_not_059()
    {
        await using var rig = await Rig.Start();
        rig.World.Model.Fail = _ =>
        {
            rig.World.Clock.Advance(SyncSourcesService.RunCap + TimeSpan.FromSeconds(1));
            return ContextErrors.SourceUnavailable.With("the model is down");
        };

        var capped = await rig.Sync(Scope());
        var sync = (await Linear(rig)).Sync;

        Assert.Equal(ContextErrors.SourceUnavailable.Code, capped.Code);
        Assert.Equal((SourceState.Unavailable, ContextErrors.SourceUnavailable.Code), (sync?.State, sync?.Code));   // the outage, not "ok / 059, the next run continues"
    }

    [Fact]
    public async Task A_failed_read_of_the_queued_records_fails_the_run_with_its_code_and_reads_nothing()
    {
        await using var rig = await Rig.Start();
        rig.World.Intents.QueuedFailure = ContextErrors.StoreUnavailable.With("the queued read failed");

        var row = await rig.Sync(Scope());

        Assert.Equal(ContextErrors.StoreUnavailable.Code, row.Code);
        Assert.Empty((await rig.Layer()).Records);   // not "nothing waiting": a record still waiting for extraction would be skipped as unchanged
    }
}
