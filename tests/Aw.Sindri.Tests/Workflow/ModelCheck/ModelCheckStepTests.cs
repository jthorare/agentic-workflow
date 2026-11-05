using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.ModelCheck;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.ModelCheck;
using Aw.Sindri.Tests.Workflow.BuildShip;

namespace Aw.Sindri.Tests.Workflow.ModelCheck;

/// <summary>
/// 'step.model.check' (model-check design §3): one outcome per row of the outcomes table, over a fake runner that prints canned 'tlc-check:' lines and exits with a code. The result word of each
/// line decides, never the exit code. Line labels are the runner's: '&lt;Name&gt;/&lt;cfg file&gt;', and '&lt;Name&gt;/gaps/&lt;cfg file&gt;' for a witness.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ModelCheckStepTests
{
    private const string Secret = "hunter2-" + "not-a-real-credential";
    private const string RunnerPath = "/home/u/.agentic-workflow/bin/cs/s1/tlc/scripts/tlc-check.sh";

    private static readonly ModelCheckSetup _setup = new(RunnerPath, "/usr/bin:/bin", "/home/u", "/home/u/.agentic-workflow", "/home/u/.agentic-workflow/bin/cs/s1/tlc/skills/formal-verify/scripts/tlc.sh", "2g", "/tmp/aw-model-check-1");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // ---- pass and skipped

    [Fact]
    public async Task Every_cfg_passing_and_every_witness_caught_is_pass_with_bounds()
    {
        var h = new Harness().Folder(Folders.Spec("Takeover")).Folder(Folders.Spec("Retry"));
        h.Prints(0, "tlc-check: Retry/Retry.cfg: PASS distinct=88 depth=9", "tlc-check: Retry/gaps/PlantedBug.cfg: WITNESS-CAUGHT NoTimeGained", "tlc-check: Takeover/Takeover.cfg: PASS distinct=1204 depth=14", "tlc-check: Takeover/gaps/PlantedBug.cfg: WITNESS-CAUGHT NoTimeGained");

        var result = await h.Step().Run(Harness.Context(), Token);

        Assert.Equal("pass", result.Result!.Outcome.Value);
        var fact = StepRig.Out(result, "modelCheck");
        Assert.False((bool?)fact["skipped"]);
        var takeover = fact["specs"]!.AsArray().Single(static s => (string?)s!["name"] == "Takeover")!;
        Assert.Equal((1204, 14), ((int?)takeover["cfgs"]![0]!["distinct"], (int?)takeover["cfgs"]![0]!["depth"]));
        Assert.Contains("up to 2 takeovers", (string?)takeover["bounds"], StringComparison.Ordinal);   // the README's Bounds line
        Assert.Contains("MaxTakeovers = 2", takeover["constants"]!.ToJsonString(), StringComparison.Ordinal);   // the cfg's CONSTANT lines
        Assert.Equal(["Retry", "Takeover"], fact["specs"]!.AsArray().Select(static s => (string?)s!["name"]), StringComparer.Ordinal);
        Assert.Equal((1, 1), (h.Rig.Slots.Taken, h.Rig.Slots.Released));
    }

    [Fact]
    public async Task A_label_without_the_cfg_extension_still_parses()
    {
        var h = new Harness().Folder(Folders.Spec("Takeover"));
        h.Prints(0, "tlc-check: Takeover/Takeover: PASS distinct=5 depth=3", "tlc-check: Takeover/gaps/PlantedBug: WITNESS-CAUGHT NoTimeGained");   // tolerance only: the runner prints the file name, with .cfg

        var result = await h.Step().Run(Harness.Context(), Token);

        Assert.Equal("pass", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task Touched_scope_with_no_modelled_path_changed_is_skipped_and_runs_nothing()
    {
        var h = new Harness().Folder(Folders.Spec("Takeover")).Changed("src/other.cs", "docs/x.md");

        var result = await h.Step().Run(Harness.Context(scope: "touched"), Token);

        Assert.Equal("skipped", result.Result!.Outcome.Value);
        var fact = StepRig.Out(result, "modelCheck");
        Assert.True((bool?)fact["skipped"]);
        Assert.Contains("src/Timers.cs", fact["looked"]!.ToJsonString(), StringComparison.Ordinal);   // the files it looked for
        Assert.Empty(h.Rig.Commands.Calls);
        Assert.Equal(0, h.Rig.Slots.Taken);
    }

    [Theory]
    [InlineData("src/Timers.cs")]   // README says 'src/Timers.cs:57': the ':line' is ignored
    [InlineData("src/dir/deep/File.cs")]   // 'src/dir/' names a folder
    [InlineData("specs/tla/Takeover/Takeover.tla")]   // the folder's own files
    public async Task Touched_scope_matches_the_path_only_a_dir_prefix_and_the_specs_own_files(string changed)
    {
        var h = new Harness().Folder(Folders.Spec("Takeover")).Changed(changed);
        h.Prints(0, "tlc-check: Takeover/Takeover.cfg: PASS distinct=5 depth=3", "tlc-check: Takeover/gaps/PlantedBug.cfg: WITNESS-CAUGHT NoTimeGained");

        var result = await h.Step().Run(Harness.Context(scope: "touched"), Token);

        Assert.Equal("pass", result.Result!.Outcome.Value);
        _ = Assert.Single(h.Rig.Commands.Calls);
    }

    [Fact]
    public async Task A_folder_with_no_models_line_is_always_checked_under_touched()
    {
        var h = new Harness().Folder(Folders.Spec("Takeover", readme: "# Takeover\nBounds: small.\n")).Changed("docs/x.md");
        h.Prints(0, "tlc-check: Takeover/Takeover.cfg: PASS distinct=5 depth=3", "tlc-check: Takeover/gaps/PlantedBug.cfg: WITNESS-CAUGHT NoTimeGained");

        var result = await h.Step().Run(Harness.Context(scope: "touched"), Token);

        Assert.Equal("pass", result.Result!.Outcome.Value);
    }

    // ---- violation, vacuous, specError

    [Fact]
    public async Task A_violation_wins_over_an_error_in_another_spec()
    {
        var h = new Harness().Folder(Folders.Spec("Alpha")).Folder(Folders.Spec("Beta"));
        h.Prints(2, "tlc-check: Alpha/Alpha.cfg: VIOLATION invariant NoTimeGained", "  | Error: Invariant NoTimeGained is violated.", $"  | State 2: remaining = 3 note={Secret}", "tlc-check: Beta/Beta.cfg: ERROR parse error at line 4");

        var result = await h.Step().Run(Harness.Context(), Token);   // exit 2 is the worst code: the result word decides

        Assert.Equal("violation", result.Result!.Outcome.Value);
        var findings = StepRig.Out(result, "findings").AsArray();
        var violation = findings.Single(static f => (string?)f!["rule"] == "MDL-VIOLATION-001")!;
        Assert.Contains("NoTimeGained", (string?)violation["message"], StringComparison.Ordinal);
        Assert.Contains("Alpha", (string?)violation["message"], StringComparison.Ordinal);
        Assert.Contains(findings, static f => (string?)f!["rule"] == "MDL-SPEC-ERROR-001" && ((string?)f["message"])!.Contains("Beta", StringComparison.Ordinal));   // the error rides along
        Assert.DoesNotContain(Secret, findings.ToJsonString(), StringComparison.Ordinal);   // a trace is scrubbed
    }

    [Fact]
    public async Task A_real_TLC_violation_reads_as_four_numbered_steps_in_the_READMEs_words_and_the_runners_warnings_are_not_results()
    {
        const string Readme = "models: src/StateTimers.cs\n# Takeover\nBounds: 1 instance, up to 2 takeovers.\n- NoTimeGained: handing an item back never gives its timer more time than it had.\nVars: tmr = ticks left on the running state timer (-1 = none) · nTake = takeovers so far\n";
        var h = new Harness().Folder(Folders.Spec("Takeover", readme: Readme));
        h.Prints(1, await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Workflow", "ModelCheck", "Fixtures", "takeover-violation.txt"), Token));

        var result = await h.Step().Run(Harness.Context(), Token);

        Assert.Equal("violation", result.Result!.Outcome.Value);
        var finding = StepRig.Out(result, "findings").AsArray().Single()!;   // the 'models path missing' warnings are not results, and the witness cfg that did not report is not a finding here
        Assert.Equal("MDL-VIOLATION-001", (string?)finding["rule"]);
        Assert.Contains("NoTimeGained", (string?)finding["message"], StringComparison.Ordinal);
        Assert.Contains("Takeover/Takeover.cfg", (string?)finding["message"], StringComparison.Ordinal);
        Assert.StartsWith("The model found a hole: handing an item back never gives its timer more time than it had", (string?)finding["message"], StringComparison.Ordinal);
        var steps = ((string?)finding["detail"])!.Split('\n').TakeWhile(static l => l.Length > 0).ToList();   // why: a violation finding always carries its steps
        Assert.Equal(4, steps.Count);
        Assert.Matches(@"^1\. Start: ", steps[0]);
        Assert.StartsWith("2. Elapse: ", steps[1], StringComparison.Ordinal);
        Assert.StartsWith("3. Takeover: ", steps[2], StringComparison.Ordinal);
        Assert.StartsWith("4. Handback: ", steps[3], StringComparison.Ordinal);
        Assert.Contains("ticks left on the running state timer 3 -> 2", steps[1], StringComparison.Ordinal);   // only what changed after state 1
        Assert.DoesNotContain("ended", steps[1], StringComparison.Ordinal);
        Assert.Contains("ticks left on the running state timer 2 -> -1", steps[2], StringComparison.Ordinal);
        Assert.Contains("ticks left on the running state timer -1 -> 3", steps[3], StringComparison.Ordinal);
        Assert.DoesNotContain("states generated", (string?)finding["detail"], StringComparison.Ordinal);   // the statistics after the last state are not part of the trace
    }

    [Fact]
    public async Task A_missed_or_wrong_witness_is_vacuous()
    {
        var missed = new Harness().Folder(Folders.Spec("Takeover"));
        missed.Prints(1, "tlc-check: Takeover/Takeover.cfg: PASS distinct=5 depth=3", "tlc-check: Takeover/gaps/PlantedBug.cfg: WITNESS-MISSED");
        var wrong = new Harness().Folder(Folders.Spec("Takeover"));
        wrong.Prints(1, "tlc-check: Takeover/Takeover.cfg: PASS distinct=5 depth=3", "tlc-check: Takeover/gaps/PlantedBug.cfg: WITNESS-WRONG invariant TimerStoppedWhileTakenOver (expected NoTimeGained)");

        var a = await missed.Step().Run(Harness.Context(), Token);
        var b = await wrong.Step().Run(Harness.Context(), Token);

        Assert.Equal(("vacuous", "vacuous"), (a.Result!.Outcome.Value, b.Result!.Outcome.Value));
        var first = StepRig.Out(a, "findings").AsArray().Single()!;
        Assert.Equal("MDL-WITNESS-001", (string?)first["rule"]);
        Assert.Contains("PlantedBug", (string?)first["message"], StringComparison.Ordinal);   // which planted bug the model didn't catch
        var second = (string?)StepRig.Out(b, "findings").AsArray().Single()!["message"];
        Assert.Contains("TimerStoppedWhileTakenOver", second, StringComparison.Ordinal);   // actual
        Assert.Contains("NoTimeGained", second, StringComparison.Ordinal);   // expected
    }

    [Fact]
    public async Task An_unparseable_spec_or_a_gaps_cfg_without_a_witness_line_is_specError()
    {
        var parse = new Harness().Folder(Folders.Spec("Takeover"));
        parse.Prints(2, "tlc-check: Takeover/Takeover.cfg: ERROR parse error at line 4", "  | Encountered \"Beginning of definition\" at line 4, column 1");
        var noWitness = new Harness().Folder(Folders.Spec("Takeover"));
        noWitness.Prints(2, "tlc-check: Takeover/Takeover.cfg: PASS distinct=5 depth=3", "tlc-check: Takeover/gaps/PlantedBug.cfg: ERROR gaps cfg has no '\\* witness:' line");

        var a = await parse.Step().Run(Harness.Context(), Token);
        var b = await noWitness.Step().Run(Harness.Context(), Token);

        Assert.Equal(("specError", "specError"), (a.Result!.Outcome.Value, b.Result!.Outcome.Value));
        var finding = StepRig.Out(a, "findings").AsArray().Single()!;
        Assert.Equal("MDL-SPEC-ERROR-001", (string?)finding["rule"]);
        Assert.Contains("parse error at line 4", finding.ToJsonString(), StringComparison.Ordinal);   // tlc.sh's message and the line it names
        Assert.Equal("MDL-SPEC-ERROR-001", (string?)StepRig.Out(b, "findings").AsArray().Single()!["rule"]);
    }

    [Fact]
    public async Task An_unknown_result_line_counts_as_error()
    {
        var h = new Harness().Folder(Folders.Spec("Takeover"));
        h.Prints(0, "tlc-check: Takeover/Takeover.cfg: SHRUG whatever", "tlc-check: Takeover/gaps/PlantedBug.cfg: WITNESS-CAUGHT NoTimeGained");   // exit 0 does not make it a pass

        var result = await h.Step().Run(Harness.Context(), Token);

        Assert.Equal("specError", result.Result!.Outcome.Value);
        Assert.Equal("MDL-SPEC-ERROR-001", (string?)StepRig.Out(result, "findings").AsArray().Single()!["rule"]);
    }

    // ---- tooBig and cantRun

    [Fact]
    public async Task A_timeout_or_out_of_memory_or_unrun_cfgs_is_tooBig_naming_them()
    {
        var timeout = new Harness().Folder(Folders.Spec("Takeover"));
        timeout.Prints(0, "tlc-check: Takeover/Takeover.cfg: TIMEOUT after 180 s", "tlc-check: Takeover/gaps/PlantedBug.cfg: WITNESS-CAUGHT NoTimeGained");
        var memory = new Harness().Folder(Folders.Spec("Takeover"));
        memory.Prints(2, "tlc-check: Takeover/Takeover.cfg: ERROR tlc: java.lang.OutOfMemoryError: Java heap space", "tlc-check: Takeover/gaps/PlantedBug.cfg: WITNESS-CAUGHT NoTimeGained");
        var cutOff = new Harness().Folder(Folders.Spec("Takeover"));
        cutOff.RunnerKilled("tlc-check: Takeover/Takeover.cfg: PASS distinct=5 depth=3");   // the step's time ran out: gaps/PlantedBug never ran

        var a = await timeout.Step().Run(Harness.Context(), Token);
        var b = await memory.Step().Run(Harness.Context(), Token);
        var c = await cutOff.Step().Run(Harness.Context(), Token);

        Assert.Equal(("tooBig", "tooBig", "tooBig"), (a.Result!.Outcome.Value, b.Result!.Outcome.Value, c.Result!.Outcome.Value));
        var first = StepRig.Out(a, "findings").AsArray().Single()!;
        Assert.Equal("MDL-TOOBIG-001", (string?)first["rule"]);
        Assert.Contains("Takeover", (string?)first["message"], StringComparison.Ordinal);
        Assert.Contains("make the bounds smaller", (string?)first["message"], StringComparison.Ordinal);
        Assert.Equal("MDL-TOOBIG-001", (string?)StepRig.Out(b, "findings").AsArray().Single()!["rule"]);
        Assert.Contains("Takeover/gaps/PlantedBug.cfg", StepRig.Out(c, "findings").ToJsonString(), StringComparison.Ordinal);   // an unrun cfg is named, never counted as passed
    }

    [Fact]
    public async Task Exit_3_or_a_missing_runner_is_cantRun_with_the_fix_and_the_PATH_seen()
    {
        var noJava = new Harness().Folder(Folders.Spec("Takeover"));
        noJava.Prints(3, "tlc: java not found (fix: install JDK 21)");   // no result word, only tlc.sh's fix line
        var noRunner = new Harness(_setup with { Runner = null }).Folder(Folders.Spec("Takeover"));

        var a = await noJava.Step().Run(Harness.Context(), Token);
        var b = await noRunner.Step().Run(Harness.Context(), Token);

        Assert.Equal(("cantRun", "cantRun"), (a.Result!.Outcome.Value, b.Result!.Outcome.Value));
        var first = StepRig.Out(a, "findings").AsArray().Single()!;
        Assert.Equal("MDL-TOOL-001", (string?)first["rule"]);
        Assert.Contains("install JDK 21", first.ToJsonString(), StringComparison.Ordinal);
        Assert.Contains("/usr/bin:/bin", first.ToJsonString(), StringComparison.Ordinal);   // the PATH the step saw
        var second = StepRig.Out(b, "findings").AsArray().Single()!;
        Assert.Equal("MDL-TOOL-001", (string?)second["rule"]);
        Assert.Contains("scripts/install-cs.sh", second.ToJsonString(), StringComparison.Ordinal);
        Assert.Empty(noRunner.Rig.Commands.Calls);
    }

    // ---- MDL-SPEC-001

    [Fact]
    public async Task A_folder_with_a_symlink_or_a_non_standard_module_is_MDL_SPEC_001_and_not_run()
    {
        var h = new Harness()
            .Folder(Folders.Spec("Linked", extra: new SpecFile("Evil.jar", SpecFileKind.Symlink, string.Empty)))
            .Folder(Folders.Spec("Multi", tla: "---- MODULE Multi ----\nEXTENDS Naturals,\n   TLC,\n   IOUtils\n====\n"))
            .Folder(Folders.Spec("Local", tla: "---- MODULE Local ----\nEXTENDS Naturals\nLOCAL INSTANCE IOUtils\n====\n"))
            .Folder(Folders.Spec("Instanced", tla: "---- MODULE Instanced ----\nEXTENDS Naturals\nM == INSTANCE Elsewhere\n====\n"))
            .Folder(Folders.Spec("Odd", extra: new SpecFile("Weird.class", SpecFileKind.Other, string.Empty)))
            .Folder(Folders.Spec("Fine", tla: "---- MODULE Fine ----\n\\* EXTENDS IOUtils (a comment)\n(* INSTANCE IOUtils *)\nEXTENDS Naturals, Sequences, Helpers\n====\n", extra: new SpecFile("Helpers.tla", SpecFileKind.Regular, "---- MODULE Helpers ----\n====\n")));
        h.Prints(0, "tlc-check: Fine/Fine.cfg: PASS distinct=2 depth=2", "tlc-check: Fine/gaps/PlantedBug.cfg: WITNESS-CAUGHT NoTimeGained");

        var result = await h.Step().Run(Harness.Context(), Token);

        Assert.Equal("specError", result.Result!.Outcome.Value);
        var findings = StepRig.Out(result, "findings").AsArray();
        Assert.All(findings, static f => Assert.Equal("MDL-SPEC-001", (string?)f!["rule"]));
        var named = findings.Select(static f => (string?)f!["message"] + (string?)f["path"]).ToList();
        foreach (var bad in new[] { "Linked", "Multi", "Local", "Instanced", "Odd" })
        {
            Assert.Contains(named, n => n.Contains(bad, StringComparison.Ordinal));
        }

        Assert.DoesNotContain(named, static n => n.Contains("Fine", StringComparison.Ordinal));   // comments stripped, a same-folder module allowed
        var argv = h.Rig.Commands.Calls.Single().Command.Argv;
        Assert.Contains("/work/p1/specs/tla/Fine", argv, StringComparer.Ordinal);
        Assert.DoesNotContain(argv, static a => a.Contains("Linked", StringComparison.Ordinal) || a.Contains("Multi", StringComparison.Ordinal) || a.Contains("Odd", StringComparison.Ordinal));   // a refused folder is not run
    }

    // ---- the command line and its environment

    [Fact]
    public async Task The_runner_gets_only_the_allowed_environment_and_never_jar()
    {
        var h = new Harness().Folder(Folders.Spec("Takeover")).Folder(Folders.Spec("Retry"));
        h.Prints(0, "tlc-check: Retry/Retry.cfg: PASS distinct=1 depth=1", "tlc-check: Retry/gaps/PlantedBug.cfg: WITNESS-CAUGHT NoTimeGained", "tlc-check: Takeover/Takeover.cfg: PASS distinct=1 depth=1", "tlc-check: Takeover/gaps/PlantedBug.cfg: WITNESS-CAUGHT NoTimeGained");

        _ = await h.Step().Run(Harness.Context(), Token);

        var (command, workingDirectory) = h.Rig.Commands.Calls.Single();
        var argv = command.Argv.ToList();
        Assert.Equal(("env", "-i"), (argv[0], argv[1]));   // nothing else of the host's environment reaches TLC
        var runnerAt = argv.IndexOf(RunnerPath);
        Assert.Equal(
            ["AW_STATE_DIR=/home/u/.agentic-workflow", "HOME=/home/u", "PATH=/usr/bin:/bin", "TLC_HEAP=2g", $"TLC_SH={_setup.TlcSh}", "TMPDIR=/tmp/aw-model-check-1"],
            argv[2..runnerAt].Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
        Assert.Equal(["--root", "/work/p1", "--timeout", "180", "--witnesses", "/work/p1/specs/tla/Retry", "/work/p1/specs/tla/Takeover"], argv[(runnerAt + 1)..]);   // absolute folders, name order
        Assert.DoesNotContain("--jar", argv, StringComparer.Ordinal);
        Assert.Equal("/work/p1", workingDirectory);
    }

    [Fact]
    public async Task Witnesses_false_drops_the_flag_and_specs_defaults_to_specs_tla()
    {
        var h = new Harness().Folder(Folders.Spec("Takeover"));
        h.Prints(0, "tlc-check: Takeover/Takeover.cfg: PASS distinct=1 depth=1");

        var result = await h.Step().Run(Harness.Context(witnesses: false), Token);

        Assert.Equal("pass", result.Result!.Outcome.Value);
        Assert.DoesNotContain("--witnesses", h.Rig.Commands.Calls.Single().Command.Argv, StringComparer.Ordinal);
        Assert.Equal(["specs/tla/*/"], h.Specs.Patterns);
    }

    [Fact]
    public async Task A_missing_workspace_fails_the_Step()
    {
        var h = new Harness();

        var result = await h.Step().Run(Harness.Context(workspace: false), Token);

        Assert.Contains("fact:workspace", result.Failure, StringComparison.Ordinal);
    }

    private static class Folders
    {
        /// <summary>A spec folder under specs/tla/ with a module, a cfg, a README and one witness cfg.</summary>
        internal static SpecFolder Spec(string name, string? tla = null, string? readme = null, SpecFile? extra = null)
        {
            List<SpecFile> files =
            [
                new($"{name}.tla", SpecFileKind.Regular, tla ?? $"---- MODULE {name} ----\nEXTENDS Naturals, TLC\n====\n"),
                new($"{name}.cfg", SpecFileKind.Regular, "SPECIFICATION Spec\nCONSTANTS\n    MaxTakeovers = 2\nINVARIANT TimerStoppedWhileTakenOver\n"),
                new("README.md", SpecFileKind.Regular, readme ?? $"models: src/Timers.cs:57 src/dir/\n# {name}\nBounds: 1 instance, up to 2 takeovers.\n- NoTimeGained: handing back never gives more time.\n"),
                new("gaps/PlantedBug.cfg", SpecFileKind.Regular, "\\* witness: NoTimeGained\nSPECIFICATION Spec\nINVARIANT NoTimeGained\n"),
            ];
            if (extra is not null)
            {
                files.Add(extra);
            }

            return new SpecFolder($"specs/tla/{name}", files);
        }
    }

    private sealed class Harness(ModelCheckSetup? setup = null)
    {
        internal StepRig Rig { get; } = new();

        internal FakeSpecFolders Specs { get; } = new();

        /// <summary>The Step's context: the workspace, the change b0..h1 and the 'with'.</summary>
        internal static StepContext Context(string scope = "all", bool witnesses = true, bool workspace = true)
        {
            var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["change"] = $$"""{"repo":"{{StepRig.Tree}}","base":"b0","head":"h1"}""" };
            if (workspace)
            {
                facts["workspace"] = $"\"{StepRig.Tree}\"";
            }

            return StepRig.Context("step.model.check", facts) with
            {
                Arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["scope"] = JsonSerializer.SerializeToElement(scope),
                    ["witnesses"] = JsonSerializer.SerializeToElement(witnesses),
                },
            };
        }

        internal Harness Folder(SpecFolder folder)
        {
            _ = Specs.Add(folder);
            return this;
        }

        /// <summary>The change from b0 to h1 touched these paths.</summary>
        internal Harness Changed(params string[] paths)
        {
            _ = Rig.Branch([.. paths.Select(static p => new ChangedFile(p, ChangeKind.Modified))]);
            return this;
        }

        /// <summary>The runner prints these lines and exits with <paramref name="exit"/>.</summary>
        internal void Prints(int exit, params string[] lines) =>
            Rig.Commands.Answer("model-check", CheckRun.Of(exit, timedOut: false, TimeSpan.FromSeconds(20), string.Join('\n', lines)));

        /// <summary>The runner printed these lines and then ran past the time it was given.</summary>
        internal void RunnerKilled(params string[] lines) =>
            Rig.Commands.Answer("model-check", CheckRun.Of(143, timedOut: true, TimeSpan.FromMinutes(9), string.Join('\n', lines)));

        internal ModelCheckStep Step() => new(Specs, Rig.Changes, Rig.Runner, new Mask(), setup ?? _setup);
    }

    private sealed class Mask : ITextScrubber
    {
        public string Scrub(string text) => text.Replace(Secret, "[secret]", StringComparison.Ordinal);
    }
}
