using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Prepare;
using Aw.Sindri.Tests.Workflow.Effects;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The launcher's prepare hook (SB-11): a Step's worktree is prepared after the guard and before the sandbox, against the commit its run first launched from; a refusal fails the launch.</summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LauncherPrepareTests : IDisposable
{
    private static readonly InstanceAddress _instance = InstanceAddress.From("ITM-1/spec/1");
    private static readonly RunToken _run = RunToken.For(1, 1);
    private readonly string _home = Directory.CreateTempSubdirectory("aw-launcher-prepare-").FullName;
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeSessionHost _host = new();
    private readonly FakeWorkspacePrepare _prepare = new();
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose()
    {
        Directory.Delete(_home, recursive: true);
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    private Launcher Make() =>
        new(new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode]), _ => _host, new FakeTerminalViewer(), new LauncherOptions(_home), _clock) { Prepare = _prepare, ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null) });

    private async Task<TempGitRepo> Repo()
    {
        var repo = await TempGitRepo.Create();
        _repos.Add(repo);
        return repo;
    }

    // The first launch of the run (escalation 0) or its retry on a stronger model (escalation 1): another launch id, the same instance and run, the same worktree.
    private static LaunchRequest Step(TempGitRepo repo, string worktree, int escalation, string? baseCommit = null)
    {
        var request = A.LaunchRequest().InDirectory(repo.Path).InWorktree(worktree).Unattended(new StepArgs(Model: null, Policy: null, Ref: new StepRef("build", "agent"))).OnInstance(_instance, _run).Build();
        return request with { Launch = AgentLaunches.LaunchIdFor(WorkflowInstanceId.Of(_instance), _run, escalation), BaseCommit = baseCommit };
    }

    [Fact]
    public async Task A_relaunch_uses_the_first_launch_base_commit()
    {
        var repo = await Repo();
        var worktree = Path.Combine(repo.Path, ".claude", "worktrees", "sindri-run");
        var first = await repo.Git("rev-parse", "HEAD");
        var launcher = Make();

        _ = await launcher.Launch(Step(repo, worktree, escalation: 0), TestContext.Current.CancellationToken);
        _ = await repo.Git("-c", "user.email=test@example.invalid", "-c", "user.name=test", "commit", "-q", "--allow-empty", "-m", "the agent committed");   // HEAD moves after the first launch
        _ = await launcher.Launch(Step(repo, worktree, escalation: 1), TestContext.Current.CancellationToken);

        Expect.Equal(2, _prepare.Prepared.Count);
        Expect.Equal(first, _prepare.Prepared[0].Request.BaseCommit);
        Expect.Equal(first, _prepare.Prepared[1].Request.BaseCommit);   // never HEAD as it is now
    }

    [Fact]
    public async Task A_base_commit_the_engine_passes_is_kept_for_the_relaunch()
    {
        var repo = await Repo();
        var worktree = Path.Combine(repo.Path, ".claude", "worktrees", "sindri-run");
        var launcher = Make();

        _ = await launcher.Launch(Step(repo, worktree, escalation: 0, baseCommit: "5eed5eed5eed5eed5eed5eed5eed5eed5eed5eed"), TestContext.Current.CancellationToken);
        _ = await launcher.Launch(Step(repo, worktree, escalation: 1), TestContext.Current.CancellationToken);

        Expect.Equal("5eed5eed5eed5eed5eed5eed5eed5eed5eed5eed", _prepare.Prepared[1].Request.BaseCommit);
    }

    [Fact]
    public async Task A_failed_prepare_fails_the_launch_with_its_code_and_starts_nothing()
    {
        var repo = await Repo();
        _prepare.Refuse = SandboxRefusal.For(SindriErrors.LaunchPrepareFailed, SandboxReasons.InstallFailed, "exit 1");

        var result = await Make().Launch(Step(repo, Path.Combine(repo.Path, ".claude", "worktrees", "sindri-run"), escalation: 0), TestContext.Current.CancellationToken);

        Expect.True(result is FailedLaunch { Error.Code: var code } && string.Equals(code, "SND-LAUNCH-021", StringComparison.Ordinal));
        Expect.Equal(0, _host.Started.Count);
    }

    [Fact]
    public async Task A_launch_without_a_Step_or_a_worktree_is_not_prepared()
    {
        var repo = await Repo();
        var launcher = Make();

        _ = await launcher.Launch(A.LaunchRequest().InDirectory(repo.Path).Build(), TestContext.Current.CancellationToken);   // a session the owner opens
        _ = await launcher.Launch(A.LaunchRequest().InDirectory(repo.Path).Unattended(new StepArgs(Model: null, Policy: null, Ref: new StepRef("build", "agent"))).Build() with { Launch = AgentLaunches.LaunchIdFor(WorkflowInstanceId.Of(_instance), _run, 3) }, TestContext.Current.CancellationToken);   // a Step with no worktree works in the repo itself

        Expect.Equal(0, _prepare.Prepared.Count);
    }
}
