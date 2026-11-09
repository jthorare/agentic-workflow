using Aw.Meta.Core;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Merge;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.Status;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Status;

public sealed class StatusTests
{
    private readonly MetaWorld _world = new();

    private StatusService Service() => new(_world.Config, Worlds.StatusReader(_world), _world.Values, _world.Resources, Worlds.Advisor(_world), _world.Processes, _world.Clock);

    private async Task<StatusResponse> Status()
    {
        var result = await Service().Handle(new StatusRequest(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_quiet_meta_has_nothing_to_report()
    {
        await _world.Ready();

        var status = await Status();

        Assert.Empty(status.Obligations);
        Assert.Empty(status.MergeQueue);
        Assert.Empty(status.OwnerQueue);
        Assert.Equal(0, status.Lanes.Version);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_train_board_without_an_end_line_is_stale_when_its_process_is_gone()
    {
        await _world.Ready();
        _ = await _world.Values.Set(KvKeys.Train, "TRAIN pid 2000000000 started 2026-10-15 05:00:00Z\nTRAIN main abc: position, PR, base, RESULT", TestContext.Current.CancellationToken);

        var status = await Status();

        Assert.EndsWith("TRAIN stale: process 2000000000 is gone and wrote no end line", status.Train, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_train_board_of_a_running_process_is_not_stale()
    {
        await _world.Ready();
        _ = await _world.Values.Set(KvKeys.Train, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"TRAIN pid {_world.Processes.Start()} started x"), TestContext.Current.CancellationToken);

        var status = await Status();

        Assert.DoesNotContain("stale", status.Train, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_train_board_with_an_end_line_is_never_stale()
    {
        await _world.Ready();
        _ = await _world.Values.Set(KvKeys.Train, "TRAIN pid 2000000000 started x\nTRAIN ended y", TestContext.Current.CancellationToken);

        var status = await Status();

        Assert.DoesNotContain("stale", status.Train, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_reservation_is_recomputed_from_the_queue_and_shown_with_the_builders_advice()
    {
        await _world.Ready();
        await _world.Receive("m1", "t1-api", "ready #190 079c2541");
        await _world.Receive("m2", "t1-ui", "ready #191 079c2541");
        await _world.Receive("m3", "t1-api", "ready #192 079c2541");
        _world.Resources.Resources = new HostResources(64_000_000, 32_000_000, 0, DiskFreeBytes: 200L * 1024 * 1024 * 1024);

        var status = await Status();

        Assert.Equal([190, 191, 192], status.MergeQueue.Select(p => p.Pr));
        Assert.Equal(new HeavySlots(3, 1, 3, A.At(60)), status.Heavy);
        Assert.Equal("heavy reservation: 1 of 3 slots (queue depth 3) - set it with meta-gate-reserve 3", status.Heavy!.Line());
        Assert.Equal("builders: 21 per lane (disk 200 GB free, mem 30 GB available)", status.Builders!.Line());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_reservation_that_cannot_be_kept_leaves_its_line_out_and_the_builders_advice_stays()
    {
        await _world.Ready();
        await _world.Receive("m1", "t1-api", "ready #190 079c2541");
        await _world.Receive("m2", "t1-ui", "ready #191 079c2541");
        await _world.Receive("m3", "t1-api", "ready #192 079c2541");   // depth 3 wants one slot, so the write is reached
        _world.Resources.Resources = new HostResources(64_000_000, 32_000_000, 0, DiskFreeBytes: 200L * 1024 * 1024 * 1024);
        _world.Heavy.FailWrites = true;

        var status = await Status();

        Assert.Equal(0, _world.Heavy.Writes);
        Assert.Null(status.Heavy);
        Assert.Equal("builders: 21 per lane (disk 200 GB free, mem 30 GB available)", status.Builders!.Line());
        Assert.Equal([190, 191, 192], status.MergeQueue.Select(p => p.Pr));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_host_leaves_the_advice_out_instead_of_failing()
    {
        await _world.Ready();
        _world.Resources.FailRead = true;

        var status = await Status();

        Assert.Null(status.Heavy);
        Assert.Null(status.Builders);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_kept_reservation_the_queue_has_outgrown_is_replaced_on_status()
    {
        await _world.Ready();
        _world.Heavy.Record = new HeavyReserveRecord(3, 9, A.At(-120));

        var status = await Status();

        Assert.Equal(0, status.Heavy!.Reserve);
        Assert.False(status.Heavy.Stale);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ready_pull_request_is_queued_with_its_gate_state_until_it_merges()
    {
        await _world.Ready();
        await _world.Receive("m1", "t1-api", "ready #190 079c2541");
        var ungated = await Status();
        _ = await _world.Gates.Save(new GateRecord(190, "h", "m", ["repo"], [], GateVerdict.Pass, A.At(2)), CancellationToken.None);
        var gated = await Status();
        _ = await _world.Gates.RecordMerge(new MergeRecord(190, "abcdef123", A.At(3)), CancellationToken.None);
        var merged = await Status();

        Assert.Equal([new QueuedPr(190, "t1-api", "none")], ungated.MergeQueue);
        Assert.Equal([new QueuedPr(190, "t1-api", "Pass")], gated.MergeQueue);
        Assert.Empty(merged.MergeQueue);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_merged_earlier_stays_out_of_the_queue_after_a_later_merge()
    {
        await _world.Ready();
        await _world.Receive("m1", "t1-api", "ready #190 079c2541");
        await _world.Receive("m2", "t1-context", "ready #191 079c2541");
        _ = await _world.Gates.RecordMerge(new MergeRecord(190, "abcdef123", A.At(3)), CancellationToken.None);
        _ = await _world.Gates.RecordMerge(new MergeRecord(191, "fedcba987", A.At(4)), CancellationToken.None);

        var status = await Status();

        Assert.Empty(status.MergeQueue);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_the_watch_saw_merged_or_closed_on_GitHub_is_dropped_from_the_queue()
    {
        await _world.Ready();
        await _world.Receive("m1", "t1-api", "ready #190 079c2541");
        await _world.Receive("m2", "t1-context", "ready #191 079c2541");
        await _world.Receive("m3", "t1-ui", "ready #192 079c2541");
        _ = await _world.Values.Set(KvKeys.EndedPrs, "190,191", CancellationToken.None);

        var status = await Status();

        Assert.Equal([192], status.MergeQueue.Select(p => p.Pr));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task What_is_owed_and_what_waits_for_the_owner_is_listed()
    {
        await _world.Ready();
        await _world.Receive("m1", "t1-api", "OWNER QUESTION: which db?");

        var status = await Status();

        Assert.Equal([(ObligationKind.Reply, "m1"), (ObligationKind.RearmWatch, "watch")], status.Obligations.Select(o => (o.Kind, o.Key)));
        Assert.Equal("which db?", Assert.Single(status.OwnerQueue).Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_last_health_summary_is_shown_once_health_has_run()
    {
        await _world.Ready();
        var never = await Status();
        _ = await _world.Values.Set(KvKeys.HealthLast, "2026-10-15T05:00:00Z ok: orphans=0", CancellationToken.None);

        var ran = await Status();

        Assert.Null(never.LastHealth);
        Assert.Equal("2026-10-15T05:00:00Z ok: orphans=0", ran.LastHealth);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_team_config_it_fails_with_AW_META_002()
    {
        var result = await Service().Handle(new StatusRequest(), CancellationToken.None);

        Assert.Equal(MetaErrors.NoTeamConfig.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_carries_the_lanes_context_for_its_compact_line()
    {
        await _world.Ready();
        _world.LaneFiles.Put(new Core.Lanes.LaneRecord("api", Core.Ids.SessionId.From("s-api"), Agent: null, A.At(1)));
        _world.ClaudeTranscripts.PutUsage("s-api", 487_000, A.At(5));

        var status = await Status();

        Assert.Equal(["context: api 487k (recycle)"], Host.Commands.ContextLine.Of(status.Context), StringComparer.Ordinal);
    }
}
