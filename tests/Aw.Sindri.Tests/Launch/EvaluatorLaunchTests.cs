using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The holdout evaluator's launch (LN-LCH2): its own scratch checkout at the head sha, the set read-only, the phase worktree denied, the evidence CLIs allowed.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class EvaluatorLaunchTests : IDisposable
{
    private const string Head = "0123456789abcdef0123456789abcdef01234567";
    private static readonly EvaluatorLaunch _evaluator = new(Head, "/home/s/holdout/line-1/phase-2", "/work/repo/.claude/worktrees/sindri-line-1-1.1");

    private readonly string _home = Directory.CreateTempSubdirectory("aw-evaluator-").FullName;
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeSessionHost _host = new();
    private readonly FakeScratchCheckouts _scratch = new();

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private LauncherOptions Options => new(_home) { SindriHome = "/home/s" };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Launcher Make(AgentEntry entry) =>
        new(new LauncherParts(new AgentCatalog([entry]), _ => _host, new FakeTerminalViewer(), Options, _clock) { ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null), Scratch = _scratch });

    private static LaunchRequest Request(AgentEntry entry)
    {
        var step = AgentLaunches.EvaluatorRequestFor(Run(), new AgentStepTarget(A.FirstItemId, entry.Id, "/work/repo"), _evaluator);
        return A.LaunchRequest().WithAgent(entry.Id).Unattended(step.Step!).AsEvaluator(_evaluator).Build();
    }

    private static AgentStepRun Run()
    {
        var address = InstanceAddress.From("T-9/step/1");
        return new AgentStepRun(WorkflowInstanceId.Of(address), address, RunToken.For(1, 1), new StepRef("holdout", "step.prove.holdout"), WriteScope: false);
    }

    [Fact]
    public async Task The_evaluator_runs_in_a_scratch_checkout_at_the_head_sha_and_it_is_removed_after()
    {
        var request = Request(AgentEntries.ClaudeCode);

        var result = await Make(AgentEntries.ClaudeCode).Launch(request, Token);

        _ = Assert.IsType<StartedLaunch>(result);
        var path = Path.Combine(Options.ScratchDirectory, "eval-" + request.Launch.Value);
        Expect.Equal(path, _host.Started.Single().WorkDir);   // not the phase worktree
        Expect.Equal(Head, _scratch.Checkouts[path]);

        _ = await Make(AgentEntries.ClaudeCode).Stop(request.Launch, Token);

        Expect.Equal(0, _scratch.Checkouts.Count);
        Expect.True(_scratch.Removed.SequenceEqual([path], StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_scratch_checkout_that_cannot_be_made_fails_the_launch_and_starts_nothing()
    {
        _scratch.FailWith = Core.SindriErrors.LaunchRepoMissing.With("no such commit");

        var result = await Make(AgentEntries.ClaudeCode).Launch(Request(AgentEntries.ClaudeCode), Token);

        Expect.Equal("SND-LAUNCH-004", Assert.IsType<FailedLaunch>(result).Error.Code);
        Expect.Equal(0, _host.Started.Count);
    }

    [Fact]
    public async Task A_launch_that_fails_after_the_checkout_removes_it()
    {
        _host.FailNextStart(Kernel.KernelErrors.Unavailable.With("tmux is down"));

        var result = await Make(AgentEntries.ClaudeCode).Launch(Request(AgentEntries.ClaudeCode), Token);

        _ = Assert.IsType<FailedLaunch>(result);
        Expect.Equal(0, _scratch.Checkouts.Count);
    }

    [Fact]
    public async Task The_policy_file_holds_the_set_read_only_the_phase_worktree_denied_the_evidence_cli_and_the_checkout_writable()
    {
        var request = Request(AgentEntries.ClaudeCode);

        _ = await Make(AgentEntries.ClaudeCode).Launch(request, Token);

        var text = await File.ReadAllTextAsync(StepPolicyFile.PathFor(Options.PolicyDirectory, request.Launch), Token);
        var scratch = Path.Combine(Options.ScratchDirectory, "eval-" + request.Launch.Value);
        Assert.Contains("Bash(ui-evidence:*)", text, StringComparison.Ordinal);
        Assert.Contains($"\"Edit(/{_evaluator.HoldoutPath}/**)\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain($"Read(/{_evaluator.HoldoutPath}", text, StringComparison.Ordinal);   // readable
        Assert.Contains($"\"Read(/{_evaluator.PhaseWorktree}/**)\"", text, StringComparison.Ordinal);
        Assert.Contains($"\"Edit(/{_evaluator.PhaseWorktree}/**)\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Read(//home/s/holdout/**)", text, StringComparison.Ordinal);   // the store isn't denied wholesale: the evaluator reads its own set
        Assert.Contains($"\"Write(/{scratch}/**)\"", text, StringComparison.Ordinal);
        Assert.Contains($"\"{_evaluator.HoldoutPath}\"", text, StringComparison.Ordinal);   // additionalDirectories
    }

    [Fact]
    public async Task An_entry_whose_policy_form_cannot_express_a_denied_read_is_refused_with_LAUNCH_013_and_leaves_no_checkout()
    {
        var result = await Make(AgentEntries.Codex).Launch(Request(AgentEntries.Codex), Token);

        Expect.Equal("SND-LAUNCH-013", Assert.IsType<FailedLaunch>(result).Error.Code);
        Expect.Equal(0, _host.Started.Count);
        Expect.Equal(0, _scratch.Checkouts.Count);
    }

    [Fact]
    public void A_policy_with_a_denied_or_read_only_path_is_refused_by_every_form_but_the_settings_file()
    {
        var step = new StepArgs(Model: null, new StepPolicy([], StepPermissionMode.Default, WritablePath: null, ReadOnlyPaths: ["/h"]), new StepRef("s", "b"));

        foreach (var entry in new[] { AgentEntries.Codex, AgentEntries.Cursor, AgentEntries.Aider })
        {
            Expect.Equal("SND-LAUNCH-013", StepForms.Arguments(entry, step, policyFilePath: null).Error.Code);
        }
    }

    [Fact]
    public void The_evaluators_request_has_no_commit_rights_and_names_its_set_and_denied_worktree()
    {
        var request = AgentLaunches.EvaluatorRequestFor(Run(), new AgentStepTarget(A.FirstItemId, AgentEntries.ClaudeCode.Id, "/work/repo"), _evaluator);
        var policy = request.Step!.Policy!;

        Expect.Equal(_evaluator, request.Evaluator);
        Assert.Null(request.Worktree);
        Expect.True(policy.AllowedTools.Contains("Bash(ui-evidence:*)", StringComparer.Ordinal));
        Expect.True(!policy.AllowedTools.Contains("Bash(git commit:*)", StringComparer.Ordinal));
        Expect.True(policy.ReadOnlyPaths!.SequenceEqual([_evaluator.HoldoutPath], StringComparer.Ordinal));
        Expect.True(policy.DeniedPaths!.SequenceEqual([_evaluator.PhaseWorktree], StringComparer.Ordinal));
    }
}
