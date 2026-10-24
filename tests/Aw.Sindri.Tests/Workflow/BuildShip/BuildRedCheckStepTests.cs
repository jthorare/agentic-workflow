using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Build;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>'step.build.red-check' on the block runtime (line spec §7.2, BLD-RED-001): each item's red tests must fail on the red commit.</summary>
[Lane(TestLane.Unit)]
public sealed class BuildRedCheckStepTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static StepContext Context(string redTests = """{"A1":{"tests":["T1","T2"],"commit":"h1"},"A2":{"tests":["T3"],"commit":"h1"},"A3":{"appOnly":{"signal":"SG-1"}}}""") =>
        StepRig.Context("step.build.red-check", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["activePacket"] = $$"""{"hash":"sha256:x","dir":"{{StepRig.PacketDir}}","repo":"{{StepRig.Tree}}"}""",
            ["redTests"] = redTests,
        });

    private static BuildRedCheckStep Step(StepRig rig) => new(rig.Diff, rig.Runner);

    [Fact]
    public async Task A_failing_command_is_red_the_commands_before_it_run_once_in_the_worktree_under_one_slot()
    {
        var rig = new StepRig("typecheck", "red-tests --verbose");
        _ = rig.Commands.Answer("typecheck", StepRig.Pass()).Answer("red-tests --verbose", StepRig.Fail());

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("red", result.Result!.Outcome.Value);
        Assert.Equal([["typecheck"], ["red-tests", "--verbose"]], rig.Commands.Calls.Select(static c => c.Command.Argv.ToArray()));   // each once, no test ids added
        Assert.All(rig.Commands.Calls, static c => Assert.Equal(StepRig.Tree, c.WorkingDirectory));
        Assert.Equal((1, 1), (rig.Slots.Taken, rig.Slots.Released));
    }

    [Fact]
    public async Task Every_command_passing_is_vacuous_and_one_finding_names_the_items_their_tests_and_the_last_tail()
    {
        var rig = new StepRig("typecheck", "red-tests");
        _ = rig.Commands.Answer("typecheck", StepRig.Pass("0 errors")).Answer("red-tests", StepRig.Pass("3 passed"));

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("vacuous", result.Result!.Outcome.Value);
        var finding = StepRig.Out(result, "findings").AsArray().Single()!;
        Assert.Equal("BLD-RED-001", (string?)finding["rule"]);
        var message = (string?)finding["message"];
        string[] named = ["A1", "A2", "T1", "T2", "T3"];
        Assert.All(named, id => Assert.Contains(id, message, StringComparison.Ordinal));
        Assert.DoesNotContain("A3", message, StringComparison.Ordinal);   // the appOnly item has no test
        Assert.Equal("3 passed", (string?)finding["detail"]);
    }

    [Fact]
    public async Task A_command_that_runs_out_of_time_fails_the_Step_instead_of_judging_the_tests()
    {
        var rig = new StepRig(TimeSpan.FromMinutes(30), "red-tests");   // longMax 30 min
        _ = rig.Commands.Answer("red-tests", CheckRun.Of(-1, timedOut: true, TimeSpan.FromMinutes(30), string.Empty));

        var result = await Step(rig).Run(Context(), Token);

        Assert.Contains("ran past its timeout", result.Failure, StringComparison.Ordinal);
        Assert.Contains("(fix: ", result.Failure, StringComparison.Ordinal);
        Assert.Contains("split", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_first_failing_command_is_enough_so_the_rest_do_not_run()
    {
        var rig = new StepRig("typecheck", "red-tests", "lint");
        _ = rig.Commands.Answer("typecheck", StepRig.Fail());

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("red", result.Result!.Outcome.Value);
        Assert.Equal([["typecheck"]], rig.Commands.Calls.Select(static c => c.Command.Argv.ToArray()));
        Assert.Equal((1, 1), (rig.Slots.Taken, rig.Slots.Released));
    }

    [Fact]
    public async Task A_worktree_that_moved_past_the_red_commit_fails_the_Step_and_runs_nothing()
    {
        var rig = new StepRig("red-tests");
        rig.Diff.Advance(StepRig.Tree);

        var result = await Step(rig).Run(Context(), Token);

        Assert.Contains("not the red commit h1", result.Failure, StringComparison.Ordinal);
        Assert.Empty(rig.Commands.Calls);
    }

    [Fact]
    public async Task A_profile_with_no_commands_fails_the_Step_with_the_fix_and_takes_no_slot()
    {
        var rig = new StepRig();

        var result = await Step(rig).Run(Context(), Token);

        Assert.Contains("workflow.build.commands", result.Failure, StringComparison.Ordinal);
        Assert.Equal(0, rig.Slots.Taken);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"A1":{"appOnly":{"signal":"s"},"commit":"h1"}}""")]
    public async Task No_test_to_run_fails_the_Step(string redTests)
    {
        var rig = new StepRig("red-tests");

        var result = await Step(rig).Run(Context(redTests), Token);

        Assert.Contains("no test to run", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_active_packet_repo_fails_the_Step()
    {
        var rig = new StepRig("red-tests");

        var result = await Step(rig).Run(StepRig.Context("step.build.red-check", new Dictionary<string, string>(StringComparer.Ordinal) { ["activePacket"] = """{"hash":"h","dir":"/d"}""", ["redTests"] = "{}" }), Token);

        Assert.Contains("fact:activePacket", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_dirty_worktree_fails_the_Step_and_runs_nothing()
    {
        var rig = new StepRig("red-tests");
        rig.Diff.Touch(StepRig.Tree);

        var result = await Step(rig).Run(Context(), Token);

        Assert.Contains("uncommitted", result.Failure, StringComparison.Ordinal);
        Assert.Empty(rig.Commands.Calls);
        Assert.Equal(0, rig.Slots.Taken);
    }

    [Fact]
    public async Task Red_tests_without_the_red_commit_fail_the_Step_and_run_nothing()
    {
        var rig = new StepRig("red-tests");

        var result = await Step(rig).Run(Context("""{"A1":["T1"]}"""), Token);

        Assert.Contains("name the red commit", result.Failure, StringComparison.Ordinal);
        Assert.Empty(rig.Commands.Calls);
    }

    [Fact]
    public async Task The_short_form_with_the_commit_on_another_item_is_enough()
    {
        var rig = new StepRig("red-tests");
        _ = rig.Commands.Answer("red-tests", StepRig.Fail());

        var result = await Step(rig).Run(Context("""{"A1":["T1"],"A2":{"tests":["T2"],"commit":"h1"}}"""), Token);

        Assert.Equal("red", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task Run24_s_fact_fails_the_Step_naming_the_expected_shape_and_the_field_and_runs_nothing()
    {
        var rig = new StepRig("red-tests");

        var result = await Step(rig).Run(Context("""{"commit":"h1","file":"a.sh","tests":[{"name":"x","covers":["AC-1"]}]}"""), Token);

        Assert.Null(result.Result);
        Assert.Contains("fact:redTests", result.Failure, StringComparison.Ordinal);
        Assert.Contains(RedTestsFact.Shape, result.Failure, StringComparison.Ordinal);
        Assert.Contains("commit", result.Failure, StringComparison.Ordinal);
        Assert.Contains("tests", result.Failure, StringComparison.Ordinal);
        Assert.Empty(rig.Commands.Calls);
    }

    [Fact]
    public async Task Items_naming_different_red_commits_fail_the_Step_and_run_nothing()
    {
        var rig = new StepRig("red-tests");

        var result = await Step(rig).Run(Context("""{"A1":{"tests":["T1"],"commit":"h1"},"A2":{"tests":["T2"],"commit":"h2"}}"""), Token);

        Assert.Contains("h2", result.Failure, StringComparison.Ordinal);
        Assert.Empty(rig.Commands.Calls);
    }
}
