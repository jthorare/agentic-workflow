using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Features.GateRerun;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Gate;

public sealed class GateRerunTests
{
    private const string GateTree = "/state/meta/gates/190/tree";
    private const string MainTree = "/state/meta/gates/190/main-tree";
    private const string UnitLog = "/state/meta/gates/190/1/unit.log";
    private readonly MetaWorld _world = new();

    private GateRerunService Service() => new(
        _world.Config,
        _world.Gates,
        new GateWorkspace(_world.Repo, _world.Trees, _world.Paths),
        new RerunExecutor(_world.Runner, _world.Paths),
        _world.LogFiles);

    private async Task SaveRedGate()
    {
        await _world.ReadyWithGate();
        _world.LogFiles.Put(UnitLog, "build ok\nfailed Spike.Eval_runs (23ms)\n  Expected: 3\n  Actual: 4\n");
        var steps = new List<GateStep> { new("sync-rules", GateVerdict.Pass, "/l/s.log"), new("unit", GateVerdict.Fail, UnitLog) };
        _ = await _world.Gates.Save(new GateRecord(190, "headsha", "mainsha", ["repo", "dotnet"], steps, GateVerdict.Fail, A.At(5)), CancellationToken.None);
    }

    // The test's result on the gate tree and on the main tree.
    private void Script(CommandResult onGate, CommandResult onMain)
    {
        _ = _world.Runner
            .When("--filter-method", request => request.Cwd == MainTree ? onMain : onGate)
            .When("git");
    }

    private static CommandResult Pass() => new(0, "Passed!", string.Empty, TimedOut: false);

    private static CommandResult Fail(string text = "failed Spike.Eval_runs (900ms)\n  Expected: 3\n  Actual: 4") => new(1, text, string.Empty, TimedOut: false);

    private async Task<GateRerunResponse> Rerun(string test = "Eval_runs")
    {
        var result = await Service().Handle(new GateRerunRequest(190, "unit", test), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private async Task<IReadOnlyList<TestVerdict>> Reruns() =>
        (await _world.Gates.Latest(190, CancellationToken.None)).Value!.Steps.First(s => s.Name == "unit").Reruns;

    private async Task<FlakeVerdict?> Stored(string test = "Eval_runs") =>
        (await Reruns()).Where(r => r.Test == test).Select(r => (FlakeVerdict?)r.Verdict).SingleOrDefault();

    // The unit step as the shipped team config has it: no build of its own, and exit 8 (no test matched) turned into success.
    private async Task UseShippedUnitStep()
    {
        var team = (await _world.Config.Load(CancellationToken.None)).Value;
        var scopes = team.Gate.Scopes.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var unit = new StepConfig("unit", ["dotnet", "test", "--solution", "X.slnx", "--no-build", "--ignore-exit-code", "8"], Heavy: true);
        scopes["dotnet"] = scopes["dotnet"] with { Steps = [.. scopes["dotnet"].Steps.Where(step => step.Name != "unit"), unit] };
        _ = await _world.Config.Save(team with { Gate = team.Gate with { Scopes = scopes } }, CancellationToken.None);
    }

    private async Task SaveRedGateWithTree(string tree)
    {
        await _world.ReadyWithGate();
        _world.LogFiles.Put(UnitLog, "failed Spike.Eval_runs (23ms)\n");
        var steps = new List<GateStep> { new("unit", GateVerdict.Fail, UnitLog) };
        _ = await _world.Gates.Save(new GateRecord(190, "headsha", "mainsha", ["dotnet"], steps, GateVerdict.Fail, A.At(5)) { Tree = tree }, CancellationToken.None);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gate_tree_that_holds_another_tree_than_the_gate_tested_is_refused_without_running_anything()
    {
        await SaveRedGateWithTree("testedtree");
        _ = _world.Runner.When("rev-parse HEAD^{tree}", "othertree\n");
        Script(onGate: Pass(), onMain: Pass());

        var result = await Service().Handle(new GateRerunRequest(190, "unit", "Eval_runs"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("run aw-meta gate-run 190", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_world.Runner.Requests, r => r.Argv.Contains("--filter-method", StringComparer.Ordinal));
        Assert.Empty(await Reruns());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gate_tree_that_holds_the_tested_tree_is_rerun()
    {
        await SaveRedGateWithTree("testedtree");
        _ = _world.Runner.When("rev-parse HEAD^{tree}", "testedtree\n");
        Script(onGate: Pass(), onMain: Pass());

        Assert.Equal(FlakeVerdict.Flake, (await Rerun()).Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Isolation_starts_after_the_heavy_wrapper()
    {
        var argv = new[] { _world.Paths.Heavy!, "bash", "suite.sh" };
        var executor = new RerunExecutor(_world.Runner, _world.Paths);
        _ = _world.Runner.When("suite.sh");

        _ = await executor.Run(new RerunJob(argv, "/t", "x", FilterMustMatch: false, "/log"), CancellationToken.None);

        var request = _world.Runner.Requests.Single();
        Assert.True(request.Isolated);
        Assert.Equal(1, request.IsolatedFrom);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_rerun_on_the_gate_tree_and_on_main_carries_meta_priority()
    {
        await SaveRedGate();
        Script(onGate: Fail(), onMain: Fail());

        _ = await Rerun();

        var runs = _world.Runner.Requests.Where(r => r.Argv.Contains("--filter-method", StringComparer.Ordinal)).ToList();
        Assert.Equal([GateTree, MainTree], runs.Select(r => r.Cwd).Order(StringComparer.Ordinal).Reverse(), StringComparer.Ordinal);
        Assert.All(runs, run => Assert.Equal("meta", run.Env["AW_HEAVY_PRIORITY"]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_test_that_passes_alone_is_a_flake_and_main_is_not_run()
    {
        await SaveRedGate();
        Script(onGate: Pass(), onMain: Fail());

        var response = await Rerun();

        Assert.Equal((FlakeVerdict.Flake, FlakeVerdict.Flake), (response.Verdict, await Stored()));
        Assert.Equal(1, _world.Runner.Requests.Count(r => r.Argv.Contains("--filter-method", StringComparer.Ordinal)));
        var run = _world.Runner.Requests.Single(r => r.Argv.Contains("--filter-method", StringComparer.Ordinal));
        Assert.Equal(["/bin/heavy", "dotnet", "test", "--solution", "X.slnx", "--filter-method", "*Eval_runs*"], run.Argv);
        Assert.Equal(GateTree, run.Cwd);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_test_that_fails_alone_the_way_it_fails_on_main_is_a_flake()
    {
        await SaveRedGate();
        Script(onGate: Fail(), onMain: Fail("failed Spike.Eval_runs (12ms)\n  Expected: 3\n  Actual: 4"));

        var response = await Rerun();

        Assert.Equal(FlakeVerdict.Flake, response.Verdict);
        Assert.Contains(_world.Runner.Requests, r => r.Cwd == MainTree && r.Argv.Contains("--filter-method", StringComparer.Ordinal));
        Assert.Contains("git checkout --quiet --force --detach mainsha", _world.Runner.Lines, StringComparer.Ordinal);   // the main tree is the main the gate ran on
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_test_that_fails_alone_but_passes_on_main_is_real()
    {
        await SaveRedGate();
        Script(onGate: Fail(), onMain: Pass());

        var response = await Rerun();

        Assert.Equal((FlakeVerdict.Real, FlakeVerdict.Real), (response.Verdict, await Stored()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_test_that_fails_alone_differently_from_main_is_real()
    {
        await SaveRedGate();
        Script(onGate: Fail(), onMain: Fail("failed Spike.Eval_runs\n  timeout after 30s\n  nothing else"));

        Assert.Equal(FlakeVerdict.Real, (await Rerun()).Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_known_flake_is_a_flake_even_when_it_fails_everywhere_differently()
    {
        await SaveRedGate();
        Script(onGate: Fail("failed SpikeEvalTests.Run\n  boom a"), onMain: Fail("failed SpikeEvalTests.Run\n  boom b"));

        var response = await Rerun("SpikeEvalTests.Run");

        Assert.Equal(FlakeVerdict.Flake, response.Verdict);
        Assert.Contains("known flake", response.Reason, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("nope", "unit")]
    [InlineData("sync-rules", "unit")]
    public async Task A_step_that_cannot_be_reran_or_did_not_fail_is_refused(string step, string unused)
    {
        await SaveRedGate();
        Script(onGate: Pass(), onMain: Pass());

        var result = await Service().Handle(new GateRerunRequest(190, step, "T" + unused), CancellationToken.None);

        Assert.Equal(MetaErrors.Invalid.Code, result.Error.Code);
        Assert.DoesNotContain(_world.Runner.Requests, r => r.Argv.Contains("--filter-method", StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_with_no_gate_is_refused()
    {
        await _world.ReadyWithGate();

        var result = await Service().Handle(new GateRerunRequest(190, "unit", "T"), CancellationToken.None);

        Assert.Equal(MetaErrors.Invalid.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_test_matching_the_filter_is_refused_not_called_real()
    {
        await SaveRedGate();
        Script(onGate: new CommandResult(8, "Zero tests ran", string.Empty, TimedOut: false), onMain: Pass());

        var result = await Service().Handle(new GateRerunRequest(190, "unit", "Typo"), CancellationToken.None);

        Assert.Equal(MetaErrors.Invalid.Code, result.Error.Code);
        Assert.Null(await Stored());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_filter_that_matches_nothing_is_refused_even_when_the_step_turns_exit_8_into_success()
    {
        await SaveRedGate();
        await UseShippedUnitStep();
        const string ZeroTests = "Test run summary: Zero tests ran\n  total: 0\n  failed: 0\n  succeeded: 0\n";
        Script(onGate: new CommandResult(0, ZeroTests, string.Empty, TimedOut: false), onMain: Pass());

        var result = await Service().Handle(new GateRerunRequest(190, "unit", "Eval_runs(value: 3)"), CancellationToken.None);

        Assert.Equal(MetaErrors.Invalid.Code, result.Error.Code);
        Assert.Empty(await Reruns());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_test_that_fails_alone_and_is_not_on_main_is_real()
    {
        await SaveRedGate();
        await UseShippedUnitStep();
        const string ZeroTests = "Test run summary: Zero tests ran\n  total: 0\n  failed: 0\n  succeeded: 0\n";
        Script(onGate: Fail(), onMain: new CommandResult(8, ZeroTests, string.Empty, TimedOut: false));

        var response = await Rerun();

        Assert.Equal((FlakeVerdict.Real, FlakeVerdict.Real), (response.Verdict, await Stored()));
        Assert.Contains("not on main", response.Reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rerun_that_ran_tests_and_passed_is_a_flake_under_the_shipped_step()
    {
        await SaveRedGate();
        await UseShippedUnitStep();
        Script(onGate: new CommandResult(0, "Test run summary: Passed!\n  total: 1\n  failed: 0\n", string.Empty, TimedOut: false), onMain: Pass());

        Assert.Equal(FlakeVerdict.Flake, (await Rerun()).Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_main_tree_is_built_for_the_rerun_because_nobody_built_it()
    {
        await SaveRedGate();
        await UseShippedUnitStep();
        Script(onGate: Fail(), onMain: Fail());

        _ = await Rerun();

        var gate = _world.Runner.Requests.Single(r => r.Cwd == GateTree && r.Argv.Contains("--filter-method", StringComparer.Ordinal));
        var main = _world.Runner.Requests.Single(r => r.Cwd == MainTree && r.Argv.Contains("--filter-method", StringComparer.Ordinal));
        Assert.Contains("--no-build", gate.Argv, StringComparer.Ordinal);
        Assert.DoesNotContain("--no-build", main.Argv, StringComparer.Ordinal);
    }

    // A step with a rerun template that is no dotnet command: a bash suite, as the shipped bash-scripts step.
    private async Task UseBashStep()
    {
        var team = (await _world.Config.Load(CancellationToken.None)).Value;
        var scopes = team.Gate.Scopes.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var unit = new StepConfig("unit", ["bash", "scripts/ci/bash-tests.sh"], Heavy: true, Rerun: ["bash", "{test}"]);
        scopes["dotnet"] = scopes["dotnet"] with { Steps = [.. scopes["dotnet"].Steps.Where(step => step.Name != "unit"), unit] };
        _ = await _world.Config.Save(team with { Gate = team.Gate with { Scopes = scopes } }, CancellationToken.None);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_bash_suite_is_rerun_isolated_with_the_session_variables_stripped_and_its_output_kept()
    {
        await SaveRedGate();
        await UseBashStep();
        _ = _world.Runner.When("bash scripts/x.test.sh", request => request.Cwd == MainTree ? Fail("failed on main") : Pass()).When("git");

        var response = await Rerun("scripts/x.test.sh");

        var alone = _world.Runner.Requests.Single(r => r.Cwd == GateTree && r.Argv.Contains("scripts/x.test.sh", StringComparer.Ordinal));
        Assert.True(alone.Isolated);
        Assert.Equal(GatePriority.Scrubbed, alone.UnsetEnv);
        Assert.Equal("/state/meta/gates/190/1/rerun-unit-scripts_x.test.sh-alone.log", alone.LogFile);
        Assert.Equal(alone.LogFile, response.AloneLog);
    }

    // #1900: `bash {test}` with a bare suite name exits 127 on both trees; that ran nothing, it is not a real red.
    [Fact, Trait("Lane", "unit")]
    public async Task A_rerun_whose_command_is_not_found_is_refused_not_called_real()
    {
        await SaveRedGate();
        await UseBashStep();
        var notFound = new CommandResult(127, string.Empty, "bash: sindri-pod: No such file or directory", TimedOut: false);
        _ = _world.Runner.When("bash sindri-pod", _ => notFound).When("git");

        var result = await Service().Handle(new GateRerunRequest(190, "unit", "sindri-pod"), CancellationToken.None);

        Assert.Equal(MetaErrors.Invalid.Code, result.Error.Code);
        Assert.Null(await Stored("sindri-pod"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dotnet_rerun_keeps_the_real_home_but_still_logs()
    {
        await SaveRedGate();
        Script(onGate: Fail(), onMain: Fail());

        var response = await Rerun();

        var alone = _world.Runner.Requests.Single(r => r.Cwd == GateTree && r.Argv.Contains("--filter-method", StringComparer.Ordinal));
        var main = _world.Runner.Requests.Single(r => r.Cwd == MainTree && r.Argv.Contains("--filter-method", StringComparer.Ordinal));
        Assert.False(alone.Isolated);
        Assert.Equal(GatePriority.Scrubbed, alone.UnsetEnv);
        Assert.Equal((alone.LogFile, main.LogFile), (response.AloneLog, response.MainLog));
        Assert.EndsWith("-main.log", main.LogFile, StringComparison.Ordinal);
        Assert.Contains(response.AloneLog!, response.Reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Each_test_is_classified_on_its_own()
    {
        await SaveRedGate();
        Script(onGate: Pass(), onMain: Pass());

        _ = await Rerun("Eval_runs");
        _ = await Rerun("Other_test");

        Assert.Equal([new TestVerdict("Eval_runs", FlakeVerdict.Flake), new TestVerdict("Other_test", FlakeVerdict.Flake)], await Reruns());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("Tests")]
    [InlineData("Spike")]
    public async Task A_known_flake_is_not_matched_by_a_fragment_of_its_name(string test)
    {
        await SaveRedGate();
        Script(onGate: Fail("failed A (1ms)\n  boom a"), onMain: Fail("failed B\n  boom b"));

        Assert.Equal(FlakeVerdict.Real, (await Rerun(test)).Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_request_needs_a_pull_request_a_step_and_a_test()
    {
        var validator = new GateRerunValidator();

        Assert.True(validator.Validate(new GateRerunRequest(190, "unit", "T")).IsSuccess);
        Assert.All(
            [new GateRerunRequest(0, "unit", "T"), new GateRerunRequest(1, " ", "T"), new GateRerunRequest(1, "unit", "")],
            request => Assert.Equal(MetaErrors.Invalid.Code, validator.Validate(request).Error.Code));
    }
}
