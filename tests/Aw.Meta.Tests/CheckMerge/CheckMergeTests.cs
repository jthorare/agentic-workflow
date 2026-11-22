using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Features.CheckMerge;
using Aw.Meta.Testing;
using Aw.Meta.Tests.MergeRun;

namespace Aw.Meta.Tests.CheckMerge;

public sealed class CheckMergeTests
{
    private readonly MetaWorld _world = new();

    private CheckMergeService Service() => new(new MergeReadiness(_world.Config, _world.Gates, _world.Repo, _world.LogFiles, _world.Messages));

    private async Task SaveGate(string head, string main, string[] scopes, GateVerdict verdict = GateVerdict.Pass, int pr = 190, params GateStep[] red)
    {
        GateStep[] steps = [new("sync-rules", GateVerdict.Pass, "/l/sync.log"), .. red];
        _ = await _world.Gates.Save(new GateRecord(pr, head, main, scopes, steps, verdict, A.At(5)), CancellationToken.None);
    }

    // GitHub says: the pull request is at `head`, open, ready; origin/main is at `main`.
    private void Script(int pr = 190, string head = "headsha", string main = "mainsha", bool draft = false, string state = "OPEN", bool diffFails = false, string queue = QueueAnswers.Off, bool queueFails = false)
    {
        var view = """{"headRefOid":"HEAD","isDraft":DRAFT,"title":"Add x","state":"STATE"}"""
            .Replace("HEAD", head, StringComparison.Ordinal).Replace("DRAFT", draft ? "true" : "false", StringComparison.Ordinal).Replace("STATE", state, StringComparison.Ordinal);
        _ = _world.Runner
            .When(string.Create(CultureInfo.InvariantCulture, $"gh pr view {pr} --json headRefOid,isDraft,title,state"), view)
            .When("rev-parse --verify --quiet origin/main", main + "\n")
            .When("mainsha...", diffFails ? string.Empty : "src/a.cs\n", diffFails ? 128 : 0)   // the gated diff: code, so repo and dotnet
            .When("oldmain...", "src/a.cs\n")
            .When("gh api graphql", queueFails ? string.Empty : queue, queueFails ? 1 : 0)
            .When("git fetch");
    }

    private async Task<CheckMergeResponse> Check(int pr = 190)
    {
        var result = await Service().Handle(new CheckMergeRequest(pr), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private static string[] Failed(CheckMergeResponse response) => [.. response.Checks.Where(c => !c.Ok).Select(c => c.Name)];

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_only_gate_does_not_vouch_for_a_code_change()
    {
        await _world.ReadyWithGate();
        await SaveGate("headsha", "mainsha", ["repo"]);
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["scopes"], Failed(response), StringComparer.Ordinal);
        Assert.Contains("git diff --name-only mainsha...headsha", _world.Runner.Lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_green_gate_on_the_current_head_and_main_is_ready()
    {
        await _world.ReadyWithGate();
        await SaveGate("headsha", "mainsha", ["repo", "dotnet"]);
        Script();

        var response = await Check();

        Assert.True(response.Ready);
        Assert.Equal(["gate", "gate-result", "scopes", "head", "main", "order", "draft", "state"], response.Checks.Select(c => c.Name), StringComparer.Ordinal);
        Assert.Contains("git fetch --quiet origin pull/190/head", _world.Runner.Lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_gate_it_is_refused_without_calling_git_or_gh()
    {
        await _world.ReadyWithGate();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Contains("gate", Failed(response), StringComparer.Ordinal);
        Assert.Empty(_world.Runner.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_head_that_moved_since_the_gate_is_refused()
    {
        await _world.ReadyWithGate();
        await SaveGate("old", "mainsha", ["repo", "dotnet"]);
        Script(head: "newhead");
        _ = _world.Runner.When("git diff --name-only old..newhead", "src/a.cs\n");

        Assert.Equal(["head"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_docs_only_head_move_is_fine_once_a_repo_scope_gate_passed_on_the_new_head()
    {
        await _world.ReadyWithGate();
        await SaveGate("old", "mainsha", ["repo", "dotnet"]);
        await SaveGate("newhead", "mainsha", ["repo"]);
        Script(head: "newhead");
        _ = _world.Runner.When("git diff --name-only old..newhead", "docs/a.md\nREADME.md\n");

        var response = await Check();

        Assert.True(response.Ready);
        Assert.Contains("docs-only", response.Checks.Single(c => c.Name == "head").Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_docs_only_head_move_without_a_later_gate_is_refused()
    {
        await _world.ReadyWithGate();
        await SaveGate("old", "mainsha", ["repo", "dotnet"]);
        Script(head: "newhead");
        _ = _world.Runner.When("git diff --name-only old..newhead", "docs/a.md\n");

        Assert.Equal(["head"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_later_repo_gate_does_not_excuse_a_code_change_in_the_delta()
    {
        await _world.ReadyWithGate();
        await SaveGate("old", "mainsha", ["repo", "dotnet"]);
        await SaveGate("newhead", "mainsha", ["repo"]);
        Script(head: "newhead");
        _ = _world.Runner.When("git diff --name-only old..newhead", "docs/a.md\nsrc/b.cs\n");

        Assert.Equal(["head"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Main_that_moved_since_the_gate_is_refused()
    {
        await _world.ReadyWithGate();
        await SaveGate("headsha", "oldmain", ["repo", "dotnet"]);
        Script(main: "newmain");

        Assert.Equal(["main"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Main_moved_passes_when_the_base_branch_has_a_merge_queue()
    {
        await _world.ReadyWithGate();
        await SaveTreeGate("oldmain", "tree1");
        Script(main: "newmain", queue: QueueAnswers.OnNotQueued);
        _ = _world.Runner.When("merge-tree --write-tree newmain headsha", "tree2\n");

        var response = await Check();

        Assert.Empty(Failed(response));
        Assert.Contains("merge queue re-tests on the merged tree", response.Checks.Single(c => c.Name == "main").Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Main_moved_with_a_merge_queue_still_refuses_when_the_head_conflicts_with_main()
    {
        await _world.ReadyWithGate();
        await SaveTreeGate("oldmain", "tree1");
        Script(main: "newmain", queue: QueueAnswers.OnNotQueued);
        _ = _world.Runner.When("merge-tree --write-tree newmain headsha", "tree2\n", 1);   // exit 1: a conflict

        var response = await Check();

        Assert.Equal(["main"], Failed(response));
        Assert.Contains("conflicts", response.Checks.Single(c => c.Name == "main").Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Main_moved_still_refuses_without_a_merge_queue()
    {
        await _world.ReadyWithGate();
        await SaveTreeGate("oldmain", "tree1");
        Script(main: "newmain", queue: QueueAnswers.Off);
        _ = _world.Runner.When("merge-tree --write-tree newmain headsha", "tree2\n");

        Assert.Equal(["main"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_queue_read_refuses_instead_of_guessing_when_main_moved()
    {
        await _world.ReadyWithGate();
        await SaveTreeGate("oldmain", "tree1");
        Script(main: "newmain", queueFails: true);
        _ = _world.Runner.When("merge-tree --write-tree newmain headsha", "tree2\n");

        var result = await Service().Handle(new CheckMergeRequest(190), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("AW-META-012", result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_queue_is_not_read_while_main_is_unchanged()
    {
        await _world.ReadyWithGate();
        await SaveTreeGate("mainsha", "tree1");
        Script();

        _ = await Check();

        Assert.DoesNotContain(_world.Runner.Lines, l => l.StartsWith("gh api graphql", StringComparison.Ordinal));
    }

    private async Task SaveTreeGate(string main, string tree, params StackedPr[] stack) =>
        _ = await _world.Gates.Save(new GateRecord(190, "headsha", main, ["repo", "dotnet"], [new("sync-rules", GateVerdict.Pass, "/l/sync.log")], GateVerdict.Pass, A.At(5)) { Tree = tree, Stack = stack }, CancellationToken.None);

    // An older run of the repo scope and a newer run of the dotnet scope, on the same head and main; the older one tested `olderTree`.
    private async Task SaveTwoScopedRuns(string olderTree)
    {
        _ = await _world.Gates.Save(new GateRecord(190, "headsha", "mainsha", ["repo"], [new("sync-rules", GateVerdict.Pass, "/l/sync.log")], GateVerdict.Pass, A.At(4)) { Tree = olderTree }, CancellationToken.None);
        _ = await _world.Gates.Save(new GateRecord(190, "headsha", "mainsha", ["dotnet"], [new("build", GateVerdict.Pass, "/l/b.log"), new("unit", GateVerdict.Pass, "/l/u.log")], GateVerdict.Pass, A.At(5)) { Tree = "tree1" }, CancellationToken.None);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Runs_of_different_scopes_on_the_same_tree_complete_each_other()
    {
        await _world.ReadyWithGate();
        await SaveTwoScopedRuns("tree1");
        Script();

        Assert.Empty(Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_on_another_tree_does_not_count_towards_the_scopes_of_the_chosen_run()
    {
        await _world.ReadyWithGate();
        await SaveTwoScopedRuns("tree0");
        Script();

        Assert.Equal(["scopes"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stacked_gate_is_valid_when_merging_its_head_onto_main_gives_the_tested_tree()
    {
        await _world.ReadyWithGate();
        await SaveTreeGate("mainsha", "tree1", new StackedPr(189, "a1"));
        Script();
        _ = _world.Runner.When("merge-tree --write-tree mainsha headsha", "tree1\n");

        Assert.Empty(Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stacked_gate_is_refused_when_the_tree_differs_even_on_the_same_main()
    {
        await _world.ReadyWithGate();
        await SaveTreeGate("mainsha", "tree1", new StackedPr(189, "a1"));
        Script();
        _ = _world.Runner.When("merge-tree --write-tree mainsha headsha", "tree2\n");

        Assert.Equal(["main"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unstacked_gate_whose_main_moved_is_valid_when_the_merged_tree_is_unchanged()
    {
        await _world.ReadyWithGate();
        await SaveTreeGate("oldmain", "tree1");
        Script(main: "newmain");
        _ = _world.Runner.When("merge-tree --write-tree newmain headsha", "tree1\n");

        Assert.Empty(Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_conflicting_merge_tree_refuses_a_gate_whose_main_moved()
    {
        await _world.ReadyWithGate();
        await SaveTreeGate("oldmain", "tree1");
        Script(main: "newmain");
        _ = _world.Runner.When("merge-tree --write-tree newmain headsha", "tree1\n", 1);

        Assert.Equal(["main"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unstacked_gate_on_the_same_main_never_asks_git_for_the_tree()
    {
        await _world.ReadyWithGate();
        await SaveTreeGate("mainsha", "tree1");
        Script();

        Assert.Empty(Failed(await Check()));
        Assert.DoesNotContain(_world.Runner.Requests, r => r.Argv.Contains("merge-tree", StringComparer.Ordinal));
    }

    private const string UnitLog = "/l/unit.log";

    // A red unit step whose log names the failing tests, with the reruns recorded so far.
    private async Task SaveRedUnit(string log, params (string Test, FlakeVerdict Verdict)[] reruns)
    {
        _world.LogFiles.Put(UnitLog, log);
        await SaveGate("headsha", "mainsha", ["repo", "dotnet"], GateVerdict.Fail, red: new GateStep("unit", GateVerdict.Fail, UnitLog));
        foreach (var (test, verdict) in reruns)
        {
            _ = await _world.Gates.Classify(190, 1, "unit", new TestVerdict(test, verdict), CancellationToken.None);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_red_step_is_accepted_when_every_failing_test_in_it_is_a_classified_flake()
    {
        await _world.ReadyWithGate();
        await SaveRedUnit("failed Spike.Tests.Eval_runs (20ms)\n  boom\nfailed Spike.Tests.Other(x: 1) (3ms)\n", ("Eval_runs", FlakeVerdict.Flake), ("Other", FlakeVerdict.Flake));
        Script();

        var accepted = await Check();

        Assert.True(accepted.Ready, string.Join("; ", accepted.Checks.Where(c => !c.Ok).Select(c => c.Detail)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_flake_verdict_for_one_test_does_not_clear_the_other_failing_tests_of_the_step()
    {
        await _world.ReadyWithGate();
        await SaveRedUnit("failed Spike.Tests.Eval_runs (20ms)\nfailed Spike.Tests.Real_bug (3ms)\n", ("Eval_runs", FlakeVerdict.Flake));
        Script();

        var refused = await Check();

        Assert.Equal(["gate-result"], Failed(refused));
        Assert.Contains("Spike.Tests.Real_bug", refused.Checks.Single(c => c.Name == "gate-result").Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_red_step_with_more_failures_than_names_is_refused_with_the_counts()
    {
        await _world.ReadyWithGate();
        await SaveRedUnit("failed Spike.Tests.Eval_runs (20ms)\nTest run summary: Failed!\n  total: 9\n  failed: 2\n  succeeded: 7\n", ("Eval_runs", FlakeVerdict.Flake));
        Script();

        var refused = await Check();

        Assert.Equal(["gate-result"], Failed(refused));
        Assert.Contains("2 failed", refused.Checks.Single(c => c.Name == "gate-result").Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_red_step_whose_summary_count_matches_its_names_is_accepted()
    {
        await _world.ReadyWithGate();
        await SaveRedUnit("failed Spike.Tests.Eval_runs (20ms)\nTest run summary: Failed!\n  total: 9\n  failed: 1\n  succeeded: 8\n", ("Eval_runs", FlakeVerdict.Flake));
        Script();

        Assert.True((await Check()).Ready);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_test_classified_real_or_not_classified_keeps_the_gate_red()
    {
        await _world.ReadyWithGate();
        await SaveRedUnit("failed Spike.Tests.Eval_runs (20ms)\n", ("Eval_runs", FlakeVerdict.Real));
        Script();

        Assert.Equal(["gate-result"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_red_step_whose_failing_tests_cannot_be_read_is_refused_even_with_a_flake_verdict()
    {
        await _world.ReadyWithGate();
        await SaveGate("headsha", "mainsha", ["repo", "dotnet"], GateVerdict.Fail, red: new GateStep("unit", GateVerdict.Fail, "/l/missing.log"));
        _ = await _world.Gates.Classify(190, 1, "unit", new TestVerdict("Eval_runs", FlakeVerdict.Flake), CancellationToken.None);
        Script();

        var refused = await Check();

        Assert.Equal(["gate-result"], Failed(refused));
        Assert.Contains("could not list its failing tests", refused.Checks.Single(c => c.Name == "gate-result").Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_red_step_with_a_per_assembly_summary_and_error_noise_is_accepted_when_its_one_failure_is_a_flake()
    {
        await _world.ReadyWithGate();
        await SaveRedUnit(await Golden("mtp-one-failed-assembly-summary.log"), ("Stderr_output_does_not_hide_the_version", FlakeVerdict.Flake));
        Script();

        var accepted = await Check();

        Assert.True(accepted.Ready, string.Join("; ", accepted.Checks.Where(c => !c.Ok).Select(c => c.Detail)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_red_step_whose_log_is_larger_than_four_mebibytes_is_still_read_in_full()
    {
        await _world.ReadyWithGate();
        var noise = string.Concat(Enumerable.Repeat("  time=2026-10-18T14:07:00.419 level=ERROR msg=\"Operation failed with internal error.\" error=\"CreateTaskQueue operation failed.\"\n", 40_000));
        await SaveRedUnit(noise + await Golden("mtp-one-failed-assembly-summary.log") + noise, ("Stderr_output_does_not_hide_the_version", FlakeVerdict.Flake));
        Script();

        var accepted = await Check();

        Assert.True(accepted.Ready, string.Join("; ", accepted.Checks.Where(c => !c.Ok).Select(c => c.Detail)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_red_step_whose_assembly_failed_with_no_test_line_stays_unvouchable()
    {
        await _world.ReadyWithGate();
        await SaveRedUnit("/g/bin/Aw.Sindri.Tests.dll (net10.0|x64) failed with 1 error(s) (1s 2ms)\nTest run summary: Failed!\n  /g/bin/Aw.Sindri.Tests.dll (net10.0|x64) failed with 1 error(s) (1s 2ms)\nExit code: 2\n", ("Eval_runs", FlakeVerdict.Flake));
        Script();

        var refused = await Check();

        Assert.Equal(["gate-result"], Failed(refused));
        Assert.Contains("could not list its failing tests", refused.Checks.Single(c => c.Name == "gate-result").Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_red_step_whose_assembly_summary_counts_more_errors_than_failed_lines_is_refused_with_the_counts()
    {
        await _world.ReadyWithGate();
        await SaveRedUnit("failed Spike.Tests.Eval_runs (20ms)\n/g/bin/A.dll (net10.0|x64) failed with 1 error(s) (1s)\n/g/bin/B.dll (net10.0|x64) failed with 1 error(s) (1s)\n", ("Eval_runs", FlakeVerdict.Flake));
        Script();

        var refused = await Check();

        Assert.Equal(["gate-result"], Failed(refused));
        Assert.Contains("2 failed", refused.Checks.Single(c => c.Name == "gate-result").Detail, StringComparison.Ordinal);
    }

    private static Task<string> Golden(string name) =>
        File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Gate", "Golden", name), TestContext.Current.CancellationToken);

    [Fact, Trait("Lane", "unit")]
    public async Task A_bash_suite_flake_vouched_by_a_rerun_is_accepted()
    {
        await _world.ReadyWithGate();
        _world.LogFiles.Put(UnitLog, await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Gate", "Golden", "bash-runner-one-failed.log"), TestContext.Current.CancellationToken));
        await SaveGate("headsha", "mainsha", ["repo", "dotnet"], GateVerdict.Fail, red: new GateStep("unit", GateVerdict.Fail, UnitLog));
        _ = await _world.Gates.Classify(190, 1, "unit", new TestVerdict("scripts/tests/ci-bash-tests.test.sh", FlakeVerdict.Flake), CancellationToken.None);
        Script();

        var accepted = await Check();

        Assert.True(accepted.Ready, string.Join("; ", accepted.Checks.Where(c => !c.Ok).Select(c => c.Detail)));
    }

    // Two gate runs at one head and main: run 1 ran repo and dotnet, run 2 re-ran only dotnet.
    private async Task SaveRun(string head, string main, string[] scopes, GateVerdict verdict, params GateStep[] red)
    {
        var sync = scopes.Contains("repo") ? new[] { new GateStep("sync-rules", GateVerdict.Pass, "/l/sync.log") } : [];
        GateStep[] steps = [.. sync, .. red];
        _ = await _world.Gates.Save(new GateRecord(190, head, main, scopes, steps, verdict, A.At(5)), CancellationToken.None);
    }

    private static GateStep RedUnit() => new("unit", GateVerdict.Fail, "/l/unit-red.log");

    private static GateStep GreenUnit() => new("unit", GateVerdict.Pass, "/l/unit-green.log");

    [Fact, Trait("Lane", "unit")]
    public async Task A_scoped_re_gate_completes_an_earlier_run_at_the_same_head_and_main()
    {
        await _world.ReadyWithGate();
        await SaveRun("headsha", "mainsha", ["repo", "dotnet"], GateVerdict.Fail, RedUnit());
        await SaveRun("headsha", "mainsha", ["dotnet"], GateVerdict.Pass, GreenUnit());
        Script();

        var response = await Check();

        Assert.True(response.Ready, string.Join("; ", response.Checks.Where(c => !c.Ok).Select(c => c.Detail)));
        var scopes = response.Checks.Single(c => c.Name == "scopes").Detail;
        Assert.Contains("repo (run 1)", scopes, StringComparison.Ordinal);
        Assert.Contains("dotnet (run 2)", scopes, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scope_whose_latest_run_failed_stays_red_even_if_an_earlier_run_passed_it()
    {
        await _world.ReadyWithGate();
        await SaveRun("headsha", "mainsha", ["repo", "dotnet"], GateVerdict.Pass, GreenUnit());
        await SaveRun("headsha", "mainsha", ["dotnet"], GateVerdict.Fail, RedUnit());
        Script();

        Assert.Equal(["gate-result"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Runs_at_another_head_or_main_do_not_count()
    {
        await _world.ReadyWithGate();
        await SaveRun("oldhead", "mainsha", ["repo", "dotnet"], GateVerdict.Pass, GreenUnit());
        await SaveRun("headsha", "oldmain", ["repo", "dotnet"], GateVerdict.Pass, GreenUnit());
        await SaveRun("headsha", "mainsha", ["dotnet"], GateVerdict.Pass, GreenUnit());
        Script();

        var response = await Check();

        Assert.Equal(["scopes"], Failed(response));
        Assert.Contains("the diff needs repo", response.Checks.Single(c => c.Name == "scopes").Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_diff_that_cannot_be_read_refuses_instead_of_assuming_the_scopes()
    {
        await _world.ReadyWithGate();
        await SaveGate("headsha", "mainsha", ["repo"]);
        Script(diffFails: true);

        var refused = await Check();

        Assert.Equal(["scopes"], Failed(refused));
        Assert.Contains("could not work out the scopes", refused.Checks.Single(c => c.Name == "scopes").Detail, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("OPEN", false)]
    [InlineData("MERGED", true)]
    public async Task A_pull_request_must_wait_for_the_one_it_is_ordered_after(string earlierState, bool ready)
    {
        await _world.ReadyWithGate();
        await SaveGate("headsha", "mainsha", ["repo", "dotnet"], pr: 192);
        Script(pr: 192);
        _ = _world.Runner.When("gh pr view 190 --json state", $$"""{"state":"{{earlierState}}"}""");

        var response = await Check(pr: 192);

        Assert.Equal(ready, response.Ready);
        Assert.Equal(ready ? [] : ["order"], Failed(response));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_draft_is_refused()
    {
        await _world.ReadyWithGate();
        await SaveGate("headsha", "mainsha", ["repo", "dotnet"]);
        Script(draft: true);

        Assert.Equal(["draft"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_that_is_no_longer_open_is_refused()
    {
        await _world.ReadyWithGate();
        await SaveGate("headsha", "mainsha", ["repo", "dotnet"]);
        Script(state: "MERGED");

        Assert.Equal(["state"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_pull_request_number_must_be_positive() =>
        Assert.Equal(MetaErrors.Invalid.Code, new CheckMergeValidator().Validate(new CheckMergeRequest(0)).Error.Code);
}
