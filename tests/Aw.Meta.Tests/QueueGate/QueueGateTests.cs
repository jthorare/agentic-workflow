using Aw.Meta.Infrastructure.Files;
using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Features.QueueGate;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.QueueGate;

public sealed class QueueGateTests
{
    private readonly MetaWorld _world = new();

    private QueueGateService Service() => new(
        new TrainSelection(_world.Config, Worlds.Queue(_world)),
        new GateWorkspace(_world.Repo, _world.Trees, _world.Paths, new FileLogFiles()),
        new GateStepRunner(_world.Runner, _world.Paths),
        _world.Gates,
        _world.Values,
        Worlds.Reservation(_world),
        _world.Clock);

    private async Task SaveGate(int? parallel, params StepConfig[] steps)
    {
        await _world.Ready();
        var gate = new GateConfig(
            ["repo"],
            [],
            new Dictionary<string, ScopeConfig>(StringComparer.Ordinal) { ["repo"] = new(["**"], steps) },
            parallel);
        _ = await _world.Config.Save(new TeamConfig("t1", "/work", ["meta", "api", "ui"], "meta") { Gate = gate }, CancellationToken.None);
    }

    private static string N(int pr) => pr.ToString(CultureInfo.InvariantCulture);

    private void ScriptPr(params int[] prs)
    {
        foreach (var pr in prs)
        {
            _ = _world.Runner
                .When($"gh pr view {N(pr)} ", $$"""{"headRefOid":"head{{N(pr)}}","headRefName":"feat/{{N(pr)}}","isDraft":false,"title":"Add {{N(pr)}}"}""")
                .When($"git diff --name-only origin/main...head{N(pr)}", "src/a.cs\n");
        }
    }

    private void ScriptGit() =>
        _world.Runner
            .When("scripts/check.sh")
            .When("rev-parse --verify --quiet origin/main", "mainsha\n")
            .When("rev-parse HEAD^{tree}", "tree1\n")
            .When("git");

    private async Task<QueueGateResponse> Run(params int[] prs) => await RunMax(max: null, prs);

    private async Task<QueueGateResponse> RunMax(int? max, params int[] prs)
    {
        var result = await Service().Handle(new QueueGateRequest(prs, max), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Empty(result.Value.Entries.Where(e => e.State == TrainState.Error).Select(e => e.Note));
        return result.Value;
    }

    private List<string> MergesIn(int pr) =>
        [.. _world.Runner.Requests
            .Where(r => r.Cwd == $"/state/meta/gates/{N(pr)}/tree" && r.Argv.Contains("--no-edit", StringComparer.Ordinal))
            .Select(r => r.Argv[^1])];

    private static CommandResult Exit(int code) => new(code, string.Empty, string.Empty, TimedOut: false);

    [Fact, Trait("Lane", "unit")]
    public async Task Each_gate_stacks_the_heads_ahead_of_it_in_order_on_one_pinned_main_and_records_its_stack()
    {
        await SaveGate(parallel: null, new StepConfig("check", ["scripts/check.sh"]));
        ScriptPr(11, 12, 13);
        ScriptGit();

        var response = await Run(11, 12, 13);

        Assert.Equal(["head11"], MergesIn(11));
        Assert.Equal(["head11", "head12"], MergesIn(12));
        Assert.Equal(["head11", "head12", "head13"], MergesIn(13));
        _ = Assert.Single(_world.Runner.Lines, l => l.Contains("rev-parse --verify --quiet origin/main", StringComparison.Ordinal));
        Assert.Equal(
            [(11, "mainsha", ""), (12, "mainsha", "11:head11"), (13, "mainsha", "11:head11,12:head12")],
            response.Gates.Select(g => (g.Pr, g.Main, string.Join(',', g.Stack.Select(s => $"{N(s.Pr)}:{s.Head}")))));
        Assert.All(response.Gates, g => Assert.Equal(("tree1", GateVerdict.Pass), (g.Tree, g.Verdict)));
        Assert.Equal(
            [(1, 11, "main", TrainState.Pass), (2, 12, "main+#11", TrainState.Pass), (3, 13, "main+#11,#12", TrainState.Pass)],
            response.Entries.Select(e => (e.Position, e.Pr, e.Base, e.State)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_head_that_conflicts_is_dropped_from_the_round_and_the_rest_stack_without_it()
    {
        await SaveGate(parallel: null, new StepConfig("check", ["scripts/check.sh"]));
        ScriptPr(11, 12, 13, 14);
        _ = _world.Runner
            .When("merge --quiet --no-edit head13", exitCode: 1)
            .When("diff --name-only --diff-filter=U", "src/a.cs\n")
            .When("merge-tree --write-tree head11 head13", "treeX\n")
            .When("merge-tree --write-tree head12 head13", exitCode: 1);
        ScriptGit();

        var response = await Run(11, 12, 13, 14);

        Assert.Equal(["head11", "head12", "head14"], MergesIn(14));
        Assert.Equal([11, 12, 14], response.Gates.Select(g => g.Pr));
        Assert.Equal(
            [(1, 11, TrainState.Pass), (2, 12, TrainState.Pass), (3, 14, TrainState.Pass), (0, 13, TrainState.Dropped)],
            response.Entries.Select(e => (e.Position, e.Pr, e.State)));
        Assert.Equal("conflicts with #12 at head12", response.Entries.Single(e => e.Pr == 13).Note);
        Assert.Null((await _world.Gates.Latest(13, TestContext.Current.CancellationToken)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stacked_head_that_conflicts_with_main_itself_is_blamed_on_main_not_the_stack()
    {
        await SaveGate(parallel: null, new StepConfig("check", ["scripts/check.sh"]));
        ScriptPr(11, 12);
        _ = _world.Runner
            .When("merge --quiet --no-edit head12", exitCode: 1)
            .When("diff --name-only --diff-filter=U", "src/a.cs\n")
            .When("merge-tree --write-tree mainsha head12", exitCode: 1);
        ScriptGit();

        var response = await Run(11, 12);

        Assert.Equal(("conflicts with main at mainsha", TrainState.Dropped), (response.Entries.Single(e => e.Pr == 12).Note, response.Entries.Single(e => e.Pr == 12).State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_git_failure_while_finding_the_conflict_is_an_error_not_a_conflict()
    {
        await SaveGate(parallel: null, new StepConfig("check", ["scripts/check.sh"]));
        ScriptPr(11, 12);
        _ = _world.Runner
            .When("merge --quiet --no-edit head12", exitCode: 1)
            .When("diff --name-only --diff-filter=U", "src/a.cs\n")
            .When("merge-tree --write-tree mainsha head12", exitCode: 128);
        ScriptGit();

        var result = await Service().Handle(new QueueGateRequest([11, 12], Max: null), TestContext.Current.CancellationToken);

        var entry = result.Value.Entries.Single(e => e.Pr == 12);
        Assert.Equal(TrainState.Error, entry.State);
        Assert.Contains("merge-tree", entry.Note, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_head_that_conflicts_with_main_itself_says_so()
    {
        await SaveGate(parallel: null, new StepConfig("check", ["scripts/check.sh"]));
        ScriptPr(11);
        _ = _world.Runner.When("merge --quiet --no-edit head11", exitCode: 1).When("diff --name-only --diff-filter=U", "src/a.cs\n");
        ScriptGit();

        var response = await Run(11);

        Assert.Equal(("conflicts with main at mainsha", TrainState.Dropped), (response.Entries[0].Note, response.Entries[0].State));
        Assert.Empty(response.Gates);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task When_a_gate_fails_the_gates_above_it_that_are_still_running_are_cancelled_and_not_recorded()
    {
        await SaveGate(parallel: 3, new StepConfig("unit", ["scripts/unit.sh"], Heavy: true));
        ScriptPr(11, 12, 13);
        var aboveStarted = new TaskCompletionSource();
        var started = 0;
        var never = new TaskCompletionSource<CommandResult>();
        _ = _world.Runner.When("scripts/unit.sh", async request =>
        {
            if (request.Cwd!.Contains("/gates/11/", StringComparison.Ordinal))
            {
                await aboveStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, TestContext.Current.CancellationToken);
                return Exit(1);
            }

            if (Interlocked.Increment(ref started) == 2)
            {
                _ = aboveStarted.TrySetResult();
            }

            return await never.Task;
        });
        ScriptGit();

        var response = await Run(11, 12, 13);

        Assert.Equal([11], response.Gates.Select(g => g.Pr));
        Assert.Equal(GateVerdict.Fail, response.Gates[0].Verdict);
        Assert.Equal([TrainState.Fail, TrainState.Cancelled, TrainState.Cancelled], response.Entries.Select(e => e.State));
        Assert.Null((await _world.Gates.Latest(12, TestContext.Current.CancellationToken)).Value);
        Assert.Null((await _world.Gates.Latest(13, TestContext.Current.CancellationToken)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gate_after_a_cancelled_one_of_the_same_pull_request_gets_its_own_run_number_and_directory()
    {
        using var disk = new TempDirectory();
        var cancelled = Path.Combine(disk.Path, "11", "1");
        _ = Directory.CreateDirectory(cancelled);
        await File.WriteAllTextAsync(Path.Combine(cancelled, "check.log"), "cancelled pass\n", TestContext.Current.CancellationToken);
        await SaveGate(parallel: null, new StepConfig("check", ["scripts/check.sh"]));
        ScriptPr(11);
        ScriptGit();
        _ = _world.Runner.When("scripts/check.sh", request =>
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(request.LogFile!)!);
            File.AppendAllText(request.LogFile!, "new pass\n");
            return Exit(0);
        });
        var paths = new MetaPaths("/state/meta", disk.Path, "/bin/heavy", "gh");
        var service = new QueueGateService(
            new TrainSelection(_world.Config, Worlds.Queue(_world)),
            new GateWorkspace(_world.Repo, _world.Trees, paths, new FileLogFiles()),
            new GateStepRunner(_world.Runner, paths),
            _world.Gates,
            _world.Values,
            Worlds.Reservation(_world),
            _world.Clock);

        var result = await service.Handle(new QueueGateRequest([11], Max: null), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal(2, result.Value.Gates.Single().Run);
        Assert.Equal(2, (await _world.Gates.Latest(11, TestContext.Current.CancellationToken)).Value!.Run);
        Assert.Equal("new pass\n", await File.ReadAllTextAsync(Path.Combine(disk.Path, "11", "2", "check.log"), TestContext.Current.CancellationToken));
        Assert.Equal("cancelled pass\n", await File.ReadAllTextAsync(Path.Combine(cancelled, "check.log"), TestContext.Current.CancellationToken));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(1)]
    [InlineData(2)]
    public async Task All_the_gates_share_one_pool_of_heavy_slots(int limit)
    {
        await SaveGate(limit, new StepConfig("unit", ["scripts/unit.sh"], Heavy: true));
        ScriptPr(11, 12, 13, 14);
        var running = 0;
        var most = 0;
        _ = _world.Runner.When("scripts/unit.sh", async request =>
        {
            _ = request;
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref most, now);
            await Task.Delay(TimeSpan.FromMilliseconds(80), TimeProvider.System, TestContext.Current.CancellationToken);
            _ = Interlocked.Decrement(ref running);
            return Exit(0);
        });
        ScriptGit();

        var response = await RunMax(4, 11, 12, 13, 14);

        Assert.Equal(4, response.Gates.Count);
        Assert.Equal(limit, most);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        do
        {
            seen = Volatile.Read(ref target);
        }
        while (value > seen && Interlocked.CompareExchange(ref target, value, seen) != seen);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_train_is_left_in_the_store_for_the_status_and_a_draft_is_skipped()
    {
        await SaveGate(parallel: null, new StepConfig("check", ["scripts/check.sh"]));
        ScriptPr(11);
        _ = _world.Runner.When("gh pr view 12 ", """{"headRefOid":"head12","headRefName":"feat/12","isDraft":true,"title":"wip"}""");
        ScriptGit();

        var response = await Run(11, 12);

        Assert.Equal([TrainState.Pass, TrainState.Skipped], response.Entries.Select(e => e.State));
        var stored = (await _world.Values.Get(KvKeys.Train, TestContext.Current.CancellationToken)).Value;
        Assert.NotNull(stored);
        Assert.Contains("  1  #11  main", stored, StringComparison.Ordinal);
        Assert.Contains("SKIPPED draft", stored, StringComparison.Ordinal);
        Assert.Contains("TRAIN ended", stored, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_pull_requests_given_the_train_takes_the_merge_queue_up_to_max()
    {
        await SaveGate(parallel: null, new StepConfig("check", ["scripts/check.sh"]));
        foreach (var pr in new[] { 11, 12, 13 })
        {
            await _world.Receive($"m{N(pr)}", "t1-api", $"ready #{N(pr)} 079c25{N(pr)}");
        }

        ScriptPr(11, 12, 13);
        ScriptGit();

        var result = await Service().Handle(new QueueGateRequest([], 2), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal([11, 12], result.Value.Gates.Select(g => g.Pr));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_heavy_step_gets_as_many_wrapper_session_slots_as_the_pool_has()
    {
        await SaveGate(parallel: 3, new StepConfig("unit", ["scripts/unit.sh"], Heavy: true));
        ScriptPr(11);
        _ = _world.Runner.When("scripts/unit.sh");
        ScriptGit();

        _ = await Run(11);

        var step = _world.Runner.Requests.Single(r => r.Argv.Contains("scripts/unit.sh", StringComparer.Ordinal));
        Assert.Equal(("meta", "3"), (step.Env[GatePriority.Variable], step.Env[GatePriority.SessionSlots]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_wrapper_lock_timeout_is_an_error_for_that_gate_and_does_not_cancel_the_gates_above_it()
    {
        await SaveGate(parallel: 3, new StepConfig("unit", ["scripts/unit.sh"], Heavy: true));
        ScriptPr(11, 12);
        _ = _world.Runner.When("scripts/unit.sh", request => request.Cwd!.Contains("/gates/11/", StringComparison.Ordinal) ? Exit(75) : Exit(0));
        ScriptGit();

        var result = await Service().Handle(new QueueGateRequest([11, 12], Max: null), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal([TrainState.Error, TrainState.Pass], result.Value.Entries.Select(e => e.State));
        Assert.Equal([12], result.Value.Gates.Select(g => g.Pr));
        Assert.Null((await _world.Gates.Latest(11, TestContext.Current.CancellationToken)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cancelling_the_train_cancels_every_gate_and_the_board_gets_its_pid_and_end_line()
    {
        await SaveGate(parallel: 3, new StepConfig("unit", ["scripts/unit.sh"], Heavy: true));
        ScriptPr(11, 12);
        using var cancel = new CancellationTokenSource();
        var started = 0;
        // The fake step never looks at the caller's token: it ends only when the runner's token fires, so the train's step tokens must be linked to the caller.
        _ = _world.Runner.When("scripts/unit.sh", async _ =>
        {
            if (Interlocked.Increment(ref started) == 2)
            {
                await cancel.CancelAsync();
            }

            await Task.Delay(Timeout.Infinite, CancellationToken.None);
            return Exit(0);
        });
        ScriptGit();

        var result = await Service().Handle(new QueueGateRequest([11, 12], Max: null, Pid: 4242), cancel.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(20), TimeProvider.System, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal([TrainState.Cancelled, TrainState.Cancelled], result.Value.Entries.Select(e => e.State));
        Assert.Empty(result.Value.Gates);
        var stored = (await _world.Values.Get(KvKeys.Train, TestContext.Current.CancellationToken)).Value;
        Assert.StartsWith("TRAIN pid 4242 started", stored, StringComparison.Ordinal);
        Assert.Contains("TRAIN ended", stored, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_that_throws_is_an_error_for_its_gate_and_the_train_still_ends()
    {
        await SaveGate(parallel: 3, new StepConfig("unit", ["scripts/unit.sh"], Heavy: true));
        ScriptPr(11, 12);
        _ = _world.Runner.When("scripts/unit.sh", request => request.Cwd!.Contains("/gates/11/", StringComparison.Ordinal) ? throw new IOException("disk full") : Exit(0));
        ScriptGit();

        var result = await Service().Handle(new QueueGateRequest([11, 12], Max: null), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal([TrainState.Error, TrainState.Pass], result.Value.Entries.Select(e => e.State));
        Assert.Contains("disk full", result.Value.Entries[0].Note, StringComparison.Ordinal);
        Assert.Contains("TRAIN ended", (await _world.Values.Get(KvKeys.Train, TestContext.Current.CancellationToken)).Value, StringComparison.Ordinal);
    }
}
