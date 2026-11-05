using Aw.Meta.Infrastructure.Files;
using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Merge;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.GateRun;
using Aw.Meta.Features.MergeRun;
using Aw.Meta.Features.QueueDrop;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Reserve;

public sealed class HeavyReservationTests
{
    private readonly MetaWorld _world = new();

    private async Task Ready(int depth)
    {
        await _world.ReadyWithGate();
        for (var n = 0; n < depth; n++)
        {
            await _world.Receive("m" + n.ToString(CultureInfo.InvariantCulture), "t1-api", string.Create(CultureInfo.InvariantCulture, $"ready #{190 + n} 079c2541"));
        }
    }

    private async Task<Kernel.Result<HeavySlots?>> Apply() =>
        await Worlds.Reservation(_world).Apply((await _world.Config.Load(CancellationToken.None)).Value, CancellationToken.None);

    private async Task Configure(Func<TeamConfig, TeamConfig> change)
    {
        var team = (await _world.Config.Load(CancellationToken.None)).Value;
        _ = await _world.Config.Save(change(team), CancellationToken.None);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(5, 2)]
    [InlineData(7, 2)]
    public async Task The_reservation_follows_the_merge_queue_depth(int depth, int reserved)
    {
        await Ready(depth);

        var applied = await Apply();

        Assert.True(applied.IsSuccess);
        Assert.Equal(reserved, _world.Heavy.Reserve);
        Assert.Equal(new HeavyReserveRecord(reserved, depth, _world.Clock.GetUtcNow()), _world.Heavy.Record);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task All_means_the_teams_heavy_cap_less_the_slot_kept_for_lanes()
    {
        await Ready(7);
        await Configure(team => team with { Heavy = team.Heavy with { Cap = 5 } });

        var applied = await Apply();

        Assert.Equal(4, _world.Heavy.Reserve);
        Assert.Equal(5, applied.Value!.Cap);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Each_change_is_one_NOTE_line_naming_the_command_that_applies_it_and_an_unchanged_value_writes_nothing()
    {
        await Ready(3);

        _ = await Apply();
        _ = await Apply();

        Assert.Equal(1, _world.Heavy.Writes);
        Assert.Equal("- NOTE 2026-10-15 05:00: heavy reservation 0 -> 1 of 3 slots (merge queue depth 3); set it with meta-gate-reserve 3", Assert.Single(_world.Log.Lines));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_change_of_depth_that_keeps_the_reservation_is_recorded_without_a_NOTE_and_an_old_computation_is_renewed()
    {
        await Ready(3);
        _ = await Apply();
        await _world.Receive("m9", "t1-ui", "ready #199 079c2541");   // depth 4: still one slot
        _ = await Apply();
        _world.Clock.Advance(TimeSpan.FromMinutes(6));
        _ = await Apply();

        Assert.Equal(3, _world.Heavy.Writes);
        Assert.Equal(new HeavyReserveRecord(1, 4, _world.Clock.GetUtcNow()), _world.Heavy.Record);
        _ = Assert.Single(_world.Log.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Merged_ended_and_dropped_pull_requests_do_not_count()
    {
        await Ready(5);
        var all = await Apply();
        _ = await _world.Gates.RecordMerge(new MergeRecord(190, "abc1234", _world.Clock.GetUtcNow()), CancellationToken.None);
        _ = await _world.Values.Set(KvKeys.EndedPrs, "191", CancellationToken.None);
        _ = await _world.Values.Set(KvKeys.QueueDropped, QueueDrops.With(stored: null, 192, _world.Clock.GetUtcNow()), CancellationToken.None);

        var filtered = await Apply();

        Assert.Equal((5, 2), (all.Value!.Depth, all.Value.Reserve));
        Assert.Equal((2, 0), (filtered.Value!.Depth, filtered.Value.Reserve));   // 193 and 194 remain
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_team_that_turns_the_reservation_off_computes_and_keeps_nothing()
    {
        await Ready(7);
        await Configure(team => team with { Heavy = new HeavyConfig(ReserveSetting.Off) });

        var applied = await Apply();

        Assert.Null(applied.Value);
        Assert.Equal(0, _world.Heavy.Writes);
        Assert.Empty(_world.Log.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Custom_tiers_replace_the_defaults()
    {
        await Ready(2);
        await Configure(team => team with { Heavy = new HeavyConfig(new ReserveSetting(Enabled: true, [new ReserveTier(2, ReserveSlots.All)])) });

        _ = await Apply();

        Assert.Equal(2, _world.Heavy.Reserve);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0, 3, 0)]
    [InlineData(1, 3, 3)]
    [InlineData(2, 3, 5)]
    [InlineData(1, 2, 3)]
    [InlineData(2, 4, 5)]
    [InlineData(3, 4, 7)]
    [InlineData(4, 5, 7)]
    [InlineData(3, 5, 5)]
    public void The_printed_argument_is_the_one_that_makes_the_script_keep_the_slots(int reserve, int cap, int argument)
    {
        var slots = new HeavySlots(cap, reserve, Depth: 2, A.At(0));

        Assert.Equal(argument, slots.ScriptDepth);
        Assert.Contains("set it with meta-gate-reserve " + argument.ToString(CultureInfo.InvariantCulture), slots.Line(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_script_never_keeps_more_than_the_reservation_and_keeps_exactly_every_count_it_has_a_tier_for()
    {
        for (var cap = 1; cap <= 6; cap++)
        {
            for (var reserve = 0; reserve < cap; reserve++)
            {
                var kept = ScriptKeeps(new HeavySlots(cap, reserve, Depth: 2, A.At(0)).ScriptDepth, cap);
                var state = string.Create(CultureInfo.InvariantCulture, $"cap {cap}, reserve {reserve}: the script keeps {kept}");

                Assert.True(kept <= reserve, state);
                if (reserve <= 2 || reserve == cap - 1)
                {
                    Assert.True(kept == reserve, state);
                }
            }
        }
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void An_old_reservation_of_the_whole_cap_still_prints_7_and_the_script_keeps_the_cap_less_one(int cap)
    {
        var argument = new HeavySlots(cap, Reserve: cap, Depth: 2, A.At(0)).ScriptDepth;

        Assert.Equal(7, argument);
        Assert.Equal(cap - 1, ScriptKeeps(argument, cap));
    }

    // scripts/meta-gate-reserve.sh's tiers: 0-2 -> 0, 3-4 -> 1, 5-6 -> 2, 7 or more -> the cap, each held to the cap less one.
    private static int ScriptKeeps(int depth, int cap) =>
        Math.Min(depth switch { <= 2 => 0, <= 4 => 1, <= 6 => 2, _ => cap }, cap - 1);

    [Fact, Trait("Lane", "unit")]
    public async Task Custom_tiers_print_the_script_argument_and_not_the_queue_depth()
    {
        await Ready(2);
        await Configure(team => team with { Heavy = new HeavyConfig(new ReserveSetting(Enabled: true, [new ReserveTier(2, ReserveSlots.All)])) });

        var applied = await Apply();

        Assert.Equal("heavy reservation: 2 of 3 slots (queue depth 2) - set it with meta-gate-reserve 5", applied.Value!.Line());
        Assert.Contains("set it with meta-gate-reserve 5", Assert.Single(_world.Log.Lines), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Dropping_queued_prs_lowers_the_reservation()
    {
        await Ready(7);
        _ = await Apply();
        Assert.Equal(2, _world.Heavy.Reserve);
        var drop = new QueueDropService(_world.Messages, _world.Gates, _world.Values, _world.Log, Worlds.Reservation(_world), _world.Clock);

        foreach (var pr in new[] { 190, 191, 192, 193, 194 })
        {
            _ = await drop.Handle(new QueueDropRequest(pr, "not ready"), CancellationToken.None);
        }

        Assert.Equal(0, _world.Heavy.Reserve);
        Assert.Equal(2, _world.Heavy.Record!.Depth);
        Assert.Contains(_world.Log.Lines, l => l.Contains("heavy reservation 1 -> 0 of 3 slots (merge queue depth 2)", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gate_run_computes_the_reservation_before_its_steps_run_and_shows_it()
    {
        await Ready(3);
        var reserveWhenStepsRan = new List<int>();
        ScriptGate(() => reserveWhenStepsRan.Add(_world.Heavy.Reserve));

        var result = await GateRun().Handle(new GateRunRequest(190, ["dotnet"]), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.NotEmpty(reserveWhenStepsRan);
        Assert.All(reserveWhenStepsRan, reserved => Assert.Equal(1, reserved));
        Assert.Equal("heavy reservation: 1 of 3 slots (queue depth 3) - set it with meta-gate-reserve 3", result.Value.Heavy!.Line());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gate_run_goes_ahead_when_the_reservation_cannot_be_kept()
    {
        await Ready(3);   // depth 3 wants one slot, so Apply reaches the write
        ScriptGate();
        _world.Heavy.FailWrites = true;

        var result = await GateRun().Handle(new GateRunRequest(190, ["repo"]), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Null(result.Value.Heavy);
        Assert.Equal((0, 0), (_world.Heavy.Reserve, _world.Heavy.Writes));
        Assert.Empty(_world.Log.Lines);
        Assert.Equal(GateVerdict.Pass, result.Value.Gate.Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Merging_the_last_queued_pull_request_releases_the_reservation()
    {
        await Ready(3);
        _ = await Apply();
        Assert.Equal(1, _world.Heavy.Reserve);
        await ScriptMerge();

        var result = await MergeRun().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess && result.Value.Merged, result.IsError ? result.Error.Message : "refused");
        Assert.Equal(0, _world.Heavy.Reserve);
        Assert.Contains(_world.Log.Lines, l => l.Contains("heavy reservation 1 -> 0 of 3 slots (merge queue depth 2)", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_reservation_is_released_even_when_the_merge_line_cannot_be_logged()
    {
        await Ready(3);
        _ = await Apply();
        await ScriptMerge();
        _world.Log.FailAppends = true;

        var result = await MergeRun().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.NotNull((await _world.Gates.FindMerge(190, CancellationToken.None)).Value);   // the merge was recorded
        Assert.Equal(0, _world.Heavy.Reserve);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merge_goes_ahead_when_the_reservation_cannot_be_kept()
    {
        await Ready(3);
        await ScriptMerge();
        _world.Heavy.Record = new HeavyReserveRecord(3, 9, A.At(0));   // out of date, so the merge's recomputation reaches the write
        _world.Heavy.FailWrites = true;

        var result = await MergeRun().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess && result.Value.Merged, result.IsError ? result.Error.Message : "refused");
        Assert.Equal(3, _world.Heavy.Reserve);   // the write failed; nothing changed
        Assert.Equal(0, _world.Heavy.Writes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_gate_step_runs_with_meta_priority()
    {
        await _world.ReadyWithGate();
        ScriptGate();

        _ = await GateRun().Handle(new GateRunRequest(190, []), CancellationToken.None);

        var steps = _world.Runner.Requests.Where(r => r.LogFile is not null).ToList();
        Assert.Equal(3, steps.Count);
        Assert.All(steps, step => Assert.Equal("meta", step.Env["AW_HEAVY_PRIORITY"]));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(29, false)]
    [InlineData(31, true)]
    public async Task A_kept_computation_is_stale_after_half_an_hour(int minutes, bool stale)
    {
        await _world.Ready();
        _world.Heavy.Record = new HeavyReserveRecord(2, 5, _world.Clock.GetUtcNow() - TimeSpan.FromMinutes(minutes));
        var team = (await _world.Config.Load(CancellationToken.None)).Value;

        var advice = await Worlds.Advisor(_world).Advise(team, _world.Resources.Resources, refresh: false, CancellationToken.None);

        Assert.Equal(stale, advice.Value.Heavy!.Stale);
    }

    private GateRunService GateRun() =>
        new(_world.Config, new GateWorkspace(_world.Repo, _world.Trees, _world.Paths, new FileLogFiles()), new GateStepRunner(_world.Runner, _world.Paths), _world.Gates, Worlds.Reservation(_world), _world.Clock);

    private MergeRunService MergeRun() =>
        new(new MergeReadiness(_world.Config, _world.Gates, _world.Repo, _world.LogFiles), _world.Repo, _world.Gates, _world.Log, Worlds.Reservation(_world), _world.Clock);

    // PR 190 is gated green and ready to merge; main moves when gh merges it.
    private async Task ScriptMerge()
    {
        _ = await _world.Gates.Save(new GateRecord(190, "headsha", "mainsha", ["repo"], [new GateStep("sync-rules", GateVerdict.Pass, "/l/sync.log")], GateVerdict.Pass, _world.Clock.GetUtcNow()), CancellationToken.None);
        var merged = false;
        _ = _world.Runner
            .When("gh pr view 190 --json headRefOid,isDraft,title,state", """{"headRefOid":"headsha","isDraft":false,"title":"Add x","state":"OPEN"}""")
            .When("gh pr merge 190 --merge", _ => { merged = true; return new Core.Commands.CommandResult(0, string.Empty, string.Empty, TimedOut: false); })
            .When("rev-parse --verify --quiet origin/main", _ => new Core.Commands.CommandResult(0, merged ? "newmainsha1234\n" : "mainsha\n", string.Empty, TimedOut: false))
            .When("git diff --name-only mainsha...headsha", "README.md\n")
            .When("git fetch");
    }

    private void ScriptGate(Action? onStep = null) =>
        _ = _world.Runner
            .When("gh pr view 190", """{"headRefOid":"headsha","headRefName":"feat/x","isDraft":false,"title":"Add x"}""")
            .When("rev-parse --verify --quiet origin/main", "mainsha\n")
            .When("git diff --name-only", "src/a.cs\n")
            .When("git")
            .When("/bin/heavy", _ => { onStep?.Invoke(); return new Core.Commands.CommandResult(0, string.Empty, string.Empty, TimedOut: false); })
            .When("scripts/sync-rules.sh");
}
