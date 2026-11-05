using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Features.GateRun;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.GateRun;

public sealed class GateRunTests
{
    private const string Tree = "/state/meta/gates/190/tree";
    private readonly MetaWorld _world = new();

    private GateRunService Service() => new(
        _world.Config,
        new GateWorkspace(_world.Repo, _world.Trees, _world.Paths),
        new GateStepRunner(_world.Runner, _world.Paths),
        _world.Gates,
        Worlds.Reservation(_world),
        _world.Clock);

    private void Script(string changed = "src/a.cs\n", int unitExit = 1)
    {
        _ = _world.Runner
            .When("gh pr view 190", """{"headRefOid":"headsha","headRefName":"feat/x","isDraft":false,"title":"Add x"}""")
            .When("rev-parse --verify --quiet origin/main", "mainsha\n")
            .When("git diff --name-only origin/main...headsha", changed)
            .When("/bin/heavy dotnet test", exitCode: unitExit)
            .When("git")
            .When("/bin/heavy dotnet build")
            .When("scripts/sync-rules.sh");
    }

    private async Task<GateRunResponse> Run(params string[] scopes)
    {
        var result = await Service().Handle(new GateRunRequest(190, scopes), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_gate_merges_the_head_onto_main_in_its_tree_and_runs_the_planned_scopes()
    {
        await _world.ReadyWithGate();
        Script();

        var response = await Run();

        Assert.Equal(("headsha", "mainsha", GateVerdict.Fail), (response.Gate.Head, response.Gate.Main, response.Gate.Verdict));
        Assert.Equal(["repo", "dotnet"], response.Gate.Scopes);
        Assert.Equal(
            [("sync-rules", GateVerdict.Pass), ("build", GateVerdict.Pass), ("unit", GateVerdict.Fail)],
            response.Gate.Steps.Select(s => (s.Name, s.Verdict)));
        Assert.Contains("git fetch --quiet origin pull/190/head", _world.Runner.Lines, StringComparer.Ordinal);
        Assert.Contains(_world.Runner.Lines, l => l.EndsWith("merge --quiet --no-edit headsha", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Each_step_runs_in_the_tree_logs_under_the_run_and_heavy_ones_go_through_the_wrapper()
    {
        await _world.ReadyWithGate();
        Script();

        var response = await Run();

        var steps = _world.Runner.Requests.Where(r => r.LogFile is not null).ToList();
        Assert.Equal(
            [["scripts/sync-rules.sh", "--check"], ["/bin/heavy", "dotnet", "build", "X.slnx"], ["/bin/heavy", "dotnet", "test", "--solution", "X.slnx"]],
            steps.Select(r => r.Argv));
        Assert.All(steps, r => Assert.Equal(Tree, r.Cwd));
        Assert.Equal(
            ["/state/meta/gates/190/1/sync-rules.log", "/state/meta/gates/190/1/build.log", "/state/meta/gates/190/1/unit.log"],
            response.Gate.Steps.Select(s => s.Log),
            StringComparer.Ordinal);
    }

    private async Task SaveParallelGate(int? parallel, params StepConfig[] steps)
    {
        await _world.Ready();
        var gate = new GateConfig(
            ["repo"],
            [],
            new Dictionary<string, ScopeConfig>(StringComparer.Ordinal) { ["repo"] = new(["**"], steps) },
            parallel);
        _ = await _world.Config.Save(new TeamConfig("t1", "/work", ["meta", "api", "ui"], "meta") { Gate = gate }, CancellationToken.None);
    }

    private void ScriptGit() =>
        _world.Runner
            .When("gh pr view 190", """{"headRefOid":"headsha","headRefName":"feat/x","isDraft":false,"title":"Add x"}""")
            .When("rev-parse --verify --quiet origin/main", "mainsha\n")
            .When("git diff --name-only origin/main...headsha", "src/a.cs\n")
            .When("git");

    [Fact, Trait("Lane", "unit")]
    public async Task Independent_steps_run_together_and_the_record_keeps_config_order()
    {
        await SaveParallelGate(parallel: null, new StepConfig("a", ["scripts/a.sh"], Heavy: true), new StepConfig("b", ["scripts/b.sh"], Heavy: true));
        var bStarted = new TaskCompletionSource();
        ScriptGit();
        _ = _world.Runner
            .When("scripts/a.sh", async _ =>
            {
                await bStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, TestContext.Current.CancellationToken);
                return new CommandResult(0, string.Empty, string.Empty, TimedOut: false);
            })
            .When("scripts/b.sh", request =>
            {
                _ = request;
                _ = bStarted.TrySetResult();
                return new CommandResult(1, string.Empty, string.Empty, TimedOut: false);
            });

        var response = await Run();

        Assert.Equal([("a", GateVerdict.Pass), ("b", GateVerdict.Fail)], response.Gate.Steps.Select(s => (s.Name, s.Verdict)));
        Assert.Equal(GateVerdict.Fail, response.Gate.Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Parallel_steps_of_one_runs_the_steps_one_at_a_time()
    {
        await SaveParallelGate(parallel: 1, new StepConfig("a", ["scripts/a.sh"], Heavy: true), new StepConfig("b", ["scripts/b.sh"], Heavy: true));
        var aDone = false;
        var bSawAFinished = false;
        ScriptGit();
        _ = _world.Runner
            .When("scripts/a.sh", async _ =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), TimeProvider.System, TestContext.Current.CancellationToken);
                aDone = true;
                return new CommandResult(0, string.Empty, string.Empty, TimedOut: false);
            })
            .When("scripts/b.sh", _ =>
            {
                bSawAFinished = aDone;
                return new CommandResult(0, string.Empty, string.Empty, TimedOut: false);
            });

        _ = await Run();

        Assert.True(bSawAFinished);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_step_in_after_stops_the_gate_with_a_config_error_before_any_step_runs()
    {
        await SaveParallelGate(parallel: null, new StepConfig("a", ["scripts/a.sh"], After: ["nope"]));
        ScriptGit();

        var result = await Service().Handle(new GateRunRequest(190, []), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("nope", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_world.Runner.Requests, r => r.LogFile is not null);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Steps_do_not_inherit_the_meta_panes_session_variables()
    {
        await _world.ReadyWithGate();
        Script();

        _ = await Run();

        var steps = _world.Runner.Requests.Where(r => r.LogFile is not null).ToList();
        Assert.NotEmpty(steps);
        Assert.All(steps, r => Assert.Equal(GatePriority.Scrubbed, r.UnsetEnv));
        Assert.Contains("TMUX", GatePriority.Scrubbed, StringComparer.Ordinal);
        Assert.Contains("AW_META_TEAM", GatePriority.Scrubbed, StringComparer.Ordinal);
        Assert.Contains("AW_SINDRI_CHILD", GatePriority.Scrubbed, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_passing_gate_is_recorded_and_a_second_run_gets_the_next_number()
    {
        await _world.ReadyWithGate();
        Script(unitExit: 0);

        var first = await Run();
        var second = await Run();

        Assert.Equal(GateVerdict.Pass, first.Gate.Verdict);
        Assert.Equal((2, "/state/meta/gates/190/2/unit.log"), (second.Gate.Run, second.Gate.Steps[2].Log));
        Assert.Equal(2, (await _world.Gates.Latest(190, CancellationToken.None)).Value!.Run);
    }

    private GateRunService ServiceAt(MetaPaths paths) => new(
        _world.Config,
        new GateWorkspace(_world.Repo, _world.Trees, paths),
        new GateStepRunner(_world.Runner, paths),
        _world.Gates,
        Worlds.Reservation(_world),
        _world.Clock);

    private static void Leave(string gates, int run, string text)
    {
        var dir = Path.Combine(gates, "190", run.ToString(CultureInfo.InvariantCulture));
        _ = Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "check.log"), text);
    }

    private async Task<GateRunResponse> RunOnDisk(string gates)
    {
        await SaveParallelGate(parallel: null, new StepConfig("check", ["scripts/check.sh"]));
        ScriptGit();
        _ = _world.Runner.When("scripts/check.sh", request =>
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(request.LogFile!)!);
            File.AppendAllText(request.LogFile!, "new pass\n");
            return new CommandResult(0, string.Empty, string.Empty, TimedOut: false);
        });
        var paths = new MetaPaths("/state/meta", gates, "/bin/heavy", "gh");
        var result = await ServiceAt(paths).Handle(new GateRunRequest(190, []), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_after_a_cancelled_run_gets_its_own_run_number_and_directory()
    {
        using var disk = new TempDirectory();
        Leave(disk.Path, 1, "cancelled pass\n");

        var response = await RunOnDisk(disk.Path);

        Assert.Equal(2, response.Gate.Run);
        Assert.Equal(2, (await _world.Gates.Latest(190, CancellationToken.None)).Value!.Run);
        Assert.Equal(Path.Combine(disk.Path, "190", "2", "check.log"), response.Gate.Steps[0].Log);
        Assert.Equal("new pass\n", await File.ReadAllTextAsync(Path.Combine(disk.Path, "190", "2", "check.log"), TestContext.Current.CancellationToken));
        Assert.Equal("cancelled pass\n", await File.ReadAllTextAsync(Path.Combine(disk.Path, "190", "1", "check.log"), TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cancelled_run_after_a_recorded_run_is_skipped_by_the_next_run()
    {
        using var disk = new TempDirectory();
        _ = await _world.Gates.Save(new GateRecord(190, "old", "mainsha", ["repo"], [], GateVerdict.Pass, A.At(1)), CancellationToken.None);
        Leave(disk.Path, 1, "recorded pass\n");
        Leave(disk.Path, 2, "cancelled pass\n");

        var response = await RunOnDisk(disk.Path);

        Assert.Equal(3, response.Gate.Run);
        Assert.Equal("new pass\n", await File.ReadAllTextAsync(Path.Combine(disk.Path, "190", "3", "check.log"), TestContext.Current.CancellationToken));
        Assert.Equal("cancelled pass\n", await File.ReadAllTextAsync(Path.Combine(disk.Path, "190", "2", "check.log"), TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Scopes_named_on_the_command_line_replace_the_plan()
    {
        await _world.ReadyWithGate();
        Script();

        var response = await Run("repo");

        Assert.Equal(["repo"], response.Gate.Scopes);
        Assert.Equal(["sync-rules"], response.Gate.Steps.Select(s => s.Name), StringComparer.Ordinal);
        Assert.DoesNotContain(_world.Runner.Lines, l => l.Contains("diff --name-only", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_docs_only_change_runs_only_the_always_scopes()
    {
        await _world.ReadyWithGate();
        Script(changed: "docs/a.md\nREADME.md\n");

        var response = await Run();

        Assert.Equal(["repo"], response.Gate.Scopes);
        Assert.Equal(GateVerdict.Pass, response.Gate.Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_head_that_conflicts_with_main_is_recorded_as_a_conflict_with_no_steps()
    {
        await _world.ReadyWithGate();
        _ = _world.Runner.When("merge --quiet", exitCode: 1).When("--diff-filter=U", "a.cs\n");
        Script();

        var response = await Run();

        Assert.Equal((GateVerdict.Conflict, 0), (response.Gate.Verdict, response.Gate.Steps.Count));
        Assert.DoesNotContain(_world.Runner.Lines, l => l.Contains("sync-rules", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scope_the_config_does_not_have_is_refused_before_anything_runs()
    {
        await _world.ReadyWithGate();
        Script();

        var result = await Service().Handle(new GateRunRequest(190, ["nope"]), CancellationToken.None);

        Assert.Equal(MetaErrors.Invalid.Code, result.Error.Code);
        Assert.Empty(_world.Runner.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_git_call_is_an_error_and_records_no_gate()
    {
        await _world.ReadyWithGate();
        _ = _world.Runner.When("gh pr view", exitCode: 1);

        var result = await Service().Handle(new GateRunRequest(190, []), CancellationToken.None);

        Assert.Equal(MetaErrors.CommandFailed.Code, result.Error.Code);
        Assert.Null((await _world.Gates.Latest(190, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_pull_request_number_must_be_positive() =>
        Assert.Equal(MetaErrors.Invalid.Code, new GateRunValidator().Validate(new GateRunRequest(0, [])).Error.Code);
}
