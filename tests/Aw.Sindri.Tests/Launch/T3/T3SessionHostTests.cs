using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.T3;

/// <summary>What only the T3 host does: a thread in the item's worktree, the app link, an interrupt for a stop, and liveness from the thread.</summary>
[Lane(TestLane.Integration)]
public sealed class T3SessionHostTests : IDisposable
{
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(4);
    private readonly T3Rig _rig = new();

    public void Dispose() => _rig.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Start_runs_a_thread_in_the_items_worktree_with_the_seed_and_title()
    {
        var started = await _rig.Host().Start(_rig.Session("snd-one"), _deadline, Token);

        Expect.Equal(new HostRef(HostKind.T3, "snd-one"), started.Value);
        Expect.Equal(new T3LaunchRequest("project-1", "sindri ITM-1", "Sindri item ITM-1.", _rig.Worktree), Assert.Single(_rig.Api.Launched));
    }

    [Fact]
    public async Task Start_in_the_repo_folder_binds_no_worktree()
    {
        _ = await _rig.Host().Start(_rig.Session("snd-one", workDir: _rig.Repo), _deadline, Token);

        Expect.True(Assert.Single(_rig.Api.Launched).WorktreePath is null);
    }

    [Fact]
    public async Task Start_refuses_a_repo_no_T3_project_holds()
    {
        var outside = _rig.Session("snd-one") with { RepoPath = "/elsewhere/repo", WorkDir = "/elsewhere/repo" };

        var started = await _rig.Host().Start(outside, _deadline, Token);

        Expect.Equal("SND-LAUNCH-026", started.Error.Code);
        Expect.True(started.Error.Message.Contains("t3 project add /elsewhere/repo", StringComparison.Ordinal));
        Expect.Equal(0, _rig.Api.Launched.Count);
    }

    [Fact]
    public async Task Start_fails_as_unavailable_when_T3_is_down()
    {
        _rig.Api.Down = true;

        var started = await _rig.Host().Start(_rig.Session("snd-one"), _deadline, Token);

        Expect.Equal("SND-LAUNCH-025", started.Error.Code);
        Expect.True(!_rig.Host().Owns("snd-one"));
    }

    [Fact]
    public async Task The_attach_command_opens_the_thread_in_the_T3_app()
    {
        var host = _rig.Host();
        var started = await host.Start(_rig.Session("snd-one"), _deadline, Token);

        Expect.Sequence(["true", "t3code://threads/env-1/thread-1"], host.AttachArgv(started.Value));
    }

    [Fact]
    public async Task Stop_interrupts_the_thread_and_forgets_the_session()
    {
        var host = _rig.Host();
        var started = await host.Start(_rig.Session("snd-one"), _deadline, Token);

        var stopped = await host.Stop(started.Value, Token);

        Expect.True(stopped.IsSuccess && !host.Owns("snd-one"));
        Expect.Equal("thread-1", Assert.Single(_rig.Api.Interrupted));
    }

    [Fact]
    public async Task A_stop_T3_cannot_take_fails_and_keeps_the_session()
    {
        var host = _rig.Host();
        var started = await host.Start(_rig.Session("snd-one"), _deadline, Token);
        _rig.Api.Down = true;

        var stopped = await host.Stop(started.Value, Token);

        Expect.Equal("SND-LAUNCH-025", stopped.Error.Code);
        Expect.True(host.Owns("snd-one"));
    }

    [Fact]
    public async Task A_session_counts_as_alive_while_T3_cannot_be_asked()
    {
        var host = _rig.Host();
        _ = await host.Start(_rig.Session(LaunchSessionName.For(A.FirstLaunchId)), _deadline, Token);
        _rig.Api.Down = true;

        Expect.Equal(new HostedSession(A.FirstLaunchId, Alive: true), Assert.Single(await host.Check(Token)));
    }

    [Fact]
    public async Task A_deleted_thread_is_gone()
    {
        var host = _rig.Host();
        _ = await host.Start(_rig.Session(LaunchSessionName.For(A.FirstLaunchId)), _deadline, Token);
        _rig.Api.Delete("thread-1");

        Expect.Equal(new HostedSession(A.FirstLaunchId, Alive: false), Assert.Single(await host.Check(Token)));
    }

    [Fact]
    public async Task A_session_name_that_is_not_a_launch_is_not_reported()
    {
        var host = _rig.Host();
        _ = await host.Start(_rig.Session("snd-one"), _deadline, Token);

        Expect.Equal(0, (await host.Check(Token)).Count);
    }

    [Fact]
    public async Task A_new_host_over_the_same_map_finds_the_session_again()
    {
        var started = await _rig.Host().Start(_rig.Session("snd-one"), _deadline, Token);

        var restarted = _rig.Host();

        Expect.True(await restarted.Exists(started.Value, Token));
        Expect.Equal(2, restarted.AttachArgv(started.Value).Count);
    }

    [Fact]
    public async Task The_probe_reports_T3s_version_or_that_it_is_not_running()
    {
        var up = await _rig.Host().Probe(Token);
        _rig.Api.Down = true;
        var down = await _rig.Host().Probe(Token);

        Expect.Equal(new HostFacts(HostKind.T3, Present: true, "0.0.0-test", Problem: null), up);
        Expect.Equal(new HostFacts(HostKind.T3, Present: false, Version: null, "not-running"), down);
    }

    [Fact]
    public void An_unknown_session_has_no_attach_command() =>
        Expect.Equal(0, _rig.Host().AttachArgv(new HostRef(HostKind.T3, "snd-never")).Count);

    [Fact]
    public async Task A_name_that_could_leave_the_map_directory_starts_no_thread()
    {
        var started = await _rig.Host().Start(_rig.Session("../escape"), _deadline, Token);

        Expect.True(started.IsError);
        Expect.Equal(0, _rig.Api.Launched.Count);
    }

    [Fact]
    public async Task A_claude_code_open_runs_on_T3s_enabled_Claude_provider()
    {
        _ = await _rig.Host().Start(_rig.Session("snd-one") with { Agent = AgentId.From("claude-code") }, _deadline, Token);

        Expect.Equal(new T3Model("claudeAgent", "claude-test"), Assert.Single(_rig.Api.Launched).Model);
    }

    [Fact]
    public async Task An_agent_whose_T3_provider_is_disabled_starts_nothing()
    {
        _rig.Api.Disable("codex");

        var started = await _rig.Host().Start(_rig.Session("snd-one") with { Agent = AgentId.From("codex") }, _deadline, Token);

        Expect.Equal("SND-LAUNCH-027", started.Error.Code);
        Expect.Equal(0, _rig.Api.Launched.Count);
    }

    [Fact]
    public async Task An_added_agent_T3_has_no_driver_for_runs_on_the_projects_default()
    {
        _ = await _rig.Host().Start(_rig.Session("snd-one") with { Agent = AgentId.From("aider") }, _deadline, Token);

        Expect.True(Assert.Single(_rig.Api.Launched).Model is null);
    }

    [Fact]
    public async Task A_clone_of_a_T3_projects_repository_runs_in_that_project_bound_to_the_clone()
    {
        var other = Path.Combine(Path.GetDirectoryName(_rig.Repo)!, "elsewhere-clone");   // why: the rig's repo is never a root path
        var clone = _rig.Session("snd-one") with { RepoPath = other, WorkDir = other };
        _rig.Api.AddProject("/home/owner/agentic-workflow", "project-2", "github.com/jthorare/agentic-workflow");
        _rig.Repos.Set(other, new T3RepoIdentity(other, "github.com/jthorare/agentic-workflow"));

        _ = await _rig.Host().Start(clone, _deadline, Token);

        var launched = Assert.Single(_rig.Api.Launched);
        Expect.Equal(("project-2", (string?)other), (launched.ProjectId, launched.WorktreePath));
    }
}
