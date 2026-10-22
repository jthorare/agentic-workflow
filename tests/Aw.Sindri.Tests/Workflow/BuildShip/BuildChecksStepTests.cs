using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Build;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>'step.build.checks' on the block runtime (line spec §7.2): the scope gate and the BLD-* rules over the measured diff, then every one of the repo's commands on the head (decision A: all run, all must pass).</summary>
[Lane(TestLane.Unit)]
public sealed class BuildChecksStepTests
{
    private const string Secret = "hunter2-not-a-real-credential";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static StepContext Context() => StepRig.Context("step.build.checks", new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["workspace"] = $"\"{StepRig.Tree}\"",
        ["activePacket"] = $$"""{"hash":"sha256:x","dir":"{{StepRig.PacketDir}}","repo":"{{StepRig.Tree}}"}""",
        ["built"] = """{"head":"claimed"}""",
    });

    private static StepRig Rig() => new("typecheck", "unit --lane \"fast one\"");

    private static BuildChecksStep Step(StepRig rig) => new(rig.Diff, rig.Changes, rig.Files, rig.Runner, new Mask());

    private static ChangedFile Added(string path) => new(path, ChangeKind.Added);

    [Fact]
    public async Task A_clean_branch_passes_and_the_measured_head_not_the_claimed_one_is_built_and_the_change()
    {
        var rig = Rig().Branch([Added("src/a.cs"), Added("tests/a.test.ts"), Added("docs/x.md")]);
        _ = rig.Commands.Answer("typecheck", StepRig.Pass()).Answer("unit --lane \"fast one\"", StepRig.Pass());

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("pass", result.Result!.Outcome.Value);
        Assert.Equal("h1", (string?)StepRig.Out(result, "built")["head"]);
        Assert.Equal("b0", (string?)StepRig.Out(result, "built")["base"]);
        Assert.Equal("feat", (string?)StepRig.Out(result, "built")["commits"]![0]!["type"]);
        Assert.Equal((StepRig.Tree, "b0", "h1"), ((string?)StepRig.Out(result, "change")["repo"], (string?)StepRig.Out(result, "change")["base"], (string?)StepRig.Out(result, "change")["head"]));
        Assert.Equal([["typecheck"], ["unit", "--lane", "fast one"]], rig.Commands.Calls.Select(static c => c.Command.Argv.ToArray()));   // every command runs on the head, quotes group
        Assert.Equal((1, 1), (rig.Slots.Taken, rig.Slots.Released));
    }

    [Fact]
    public async Task A_path_outside_the_allowed_files_alone_is_out_of_scope_and_runs_nothing()
    {
        var rig = Rig().Branch([Added("src/a.cs"), new ChangedFile("etc/conf.yml", ChangeKind.Modified)]);

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("outOfScope", result.Result!.Outcome.Value);
        var finding = StepRig.Out(result, "findings").AsArray().Single()!;
        Assert.Equal(("BLD-SCOPE-001", "etc/conf.yml"), ((string?)finding["rule"], (string?)finding["path"]));
        Assert.Empty(rig.Commands.Calls);
    }

    [Fact]
    public async Task A_secret_on_an_added_line_fails_and_names_the_file_and_line_never_the_secret()
    {
        var rig = Rig().Branch([Added("src/a.cs")], [new AddedLine("src/a.cs", 7, "var t = \"" + Secret + "\";")]);

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("failed", result.Result!.Outcome.Value);
        var finding = StepRig.Out(result, "findings").AsArray().Single()!;
        Assert.Equal(("BLD-SECRET-001", "src/a.cs", 7), ((string?)finding["rule"], (string?)finding["path"], (int?)finding["line"]));
        Assert.DoesNotContain(Secret, finding.ToJsonString(), StringComparison.Ordinal);
        Assert.Empty(rig.Commands.Calls);
    }

    [Fact]
    public async Task An_existing_test_changed_without_a_test_change_entry_fails_but_a_named_one_does_not()
    {
        var rig = Rig().Branch([new ChangedFile("tests/old.test.ts", ChangeKind.Modified), new ChangedFile("tests/other.test.ts", ChangeKind.Modified)]);

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("failed", result.Result!.Outcome.Value);
        var findings = StepRig.Out(result, "findings").AsArray();
        Assert.Contains(findings, static f => (string?)f!["rule"] == "BLD-TEST-001" && (string?)f["path"] == "tests/other.test.ts");
        Assert.DoesNotContain(findings, static f => (string?)f!["path"] == "tests/old.test.ts" && (string?)f["rule"] == "BLD-TEST-001");
    }

    [Fact]
    public async Task A_merge_commit_or_an_unconventional_subject_fails()
    {
        var rig = Rig().Branch([Added("src/a.cs")], commits: [new ChangedCommit("h1aaaaaaaa", "did stuff", IsMerge: false)]);

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("failed", result.Result!.Outcome.Value);
        Assert.Equal("BLD-COMMIT-001", (string?)StepRig.Out(result, "findings")[0]!["rule"]);
    }

    [Fact]
    public async Task Every_failing_command_is_a_finding_with_its_tail_and_the_rest_still_run()
    {
        var rig = Rig().Branch([Added("src/a.cs")]);
        _ = rig.Commands.Answer("typecheck", StepRig.Fail("error TS2322")).Answer("unit --lane \"fast one\"", StepRig.Fail("T1 still fails"));

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("failed", result.Result!.Outcome.Value);
        var findings = StepRig.Out(result, "findings").AsArray();
        Assert.Equal(["BLD-COMMAND-001", "BLD-COMMAND-001"], findings.Select(static f => (string?)f!["rule"]), StringComparer.Ordinal);
        Assert.Equal(["typecheck exited 1", "unit --lane \"fast one\" exited 1"], findings.Select(static f => (string?)f!["message"]), StringComparer.Ordinal);
        Assert.Equal("error TS2322", (string?)findings[0]!["detail"]);
        Assert.Equal(2, rig.Commands.Calls.Count);
        Assert.Equal((1, 1), (rig.Slots.Taken, rig.Slots.Released));
    }

    [Fact]
    public async Task A_scope_finding_beside_a_secret_is_failed_and_carries_both()
    {
        var rig = Rig().Branch([new ChangedFile("etc/conf.yml", ChangeKind.Modified)], [new AddedLine("etc/conf.yml", 1, Secret)]);

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("failed", result.Result!.Outcome.Value);
        Assert.Equal(["BLD-SCOPE-001", "BLD-SECRET-001"], StepRig.Out(result, "findings").AsArray().Select(static f => (string?)f!["rule"]), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_packet_that_cannot_be_read_or_a_range_git_does_not_know_fails_the_Step()
    {
        var unreadable = Rig();
        var unknownRange = Rig();

        var noPacket = await Step(unreadable).Run(StepRig.Context("step.build.checks", new Dictionary<string, string>(StringComparer.Ordinal) { ["workspace"] = $"\"{StepRig.Tree}\"", ["activePacket"] = """{"hash":"h","dir":"/missing"}""" }), Token);
        var noRange = await Step(unknownRange).Run(Context(), Token);

        Assert.NotNull(noPacket.Failure);
        Assert.NotNull(noRange.Failure);
    }

    [Fact]
    public async Task A_missing_workspace_fails_the_Step()
    {
        var result = await Step(Rig()).Run(StepRig.Context("step.build.checks", new Dictionary<string, string>(StringComparer.Ordinal)), Token);

        Assert.Contains("fact:workspace", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_dirty_tree_fails_with_a_BLD_TREE_001_finding_and_runs_nothing()
    {
        var rig = Rig().Branch([Added("src/a.cs")]);
        rig.Diff.Touch(StepRig.Tree);

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("failed", result.Result!.Outcome.Value);
        Assert.Equal("BLD-TREE-001", (string?)StepRig.Out(result, "findings").AsArray().Single()!["rule"]);
        Assert.Empty(rig.Commands.Calls);
        Assert.Equal(0, rig.Slots.Taken);
    }

    [Fact]
    public async Task A_batch_longer_than_the_Steps_budget_fails_the_Step_inside_it_with_the_budget_named()
    {
        var rig = new StepRig("a", "b", "c", "d", "e", "f").Branch([Added("src/a.cs")]);
        foreach (var name in new[] { "a", "b", "c", "d", "e", "f" })
        {
            _ = rig.Commands.Answer(name, _ =>
            {
                rig.Clock.Advance(TimeSpan.FromMinutes(23));
                return StepRig.Pass();
            });
        }

        var result = await Step(rig).Run(Context(), Token);

        Assert.Contains("budget", result.Failure, StringComparison.Ordinal);
        Assert.Equal(5, rig.Commands.Calls.Count);   // 115 min of 120: the sixth never starts
        Assert.True(rig.Clock.GetUtcNow() - KnownInstants.Epoch <= TimeSpan.FromMinutes(120));   // the Step's MaxDuration
    }

    [Fact]
    public async Task A_secret_in_a_commit_subject_or_a_path_never_reaches_the_findings_or_the_built_commits()
    {
        var bad = Rig().Branch([Added($"etc/{Secret}.yml")], commits: [new ChangedCommit("h1aaaaaaaa", $"oops {Secret}", IsMerge: false)]);
        var good = Rig().Branch([Added("src/a.cs")], commits: [new ChangedCommit("h1aaaaaaaa", $"feat: add {Secret}", IsMerge: false)]);
        _ = good.Commands.Answer("typecheck", StepRig.Pass()).Answer("unit --lane \"fast one\"", StepRig.Pass());

        var failed = await Step(bad).Run(Context(), Token);
        var passed = await Step(good).Run(Context(), Token);

        Assert.Equal("failed", failed.Result!.Outcome.Value);
        Assert.DoesNotContain(Secret, StepRig.Out(failed, "findings").ToJsonString(), StringComparison.Ordinal);
        Assert.Equal("pass", passed.Result!.Outcome.Value);
        Assert.DoesNotContain(Secret, StepRig.Out(passed, "built").ToJsonString(), StringComparison.Ordinal);
    }

    private sealed class Mask : ITextScrubber
    {
        public string Scrub(string text) => text.Replace(Secret, "[secret]", StringComparison.Ordinal);
    }
}
