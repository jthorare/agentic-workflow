using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Tests.Workflow.Effects;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>A session the owner opens runs in the sandbox like a Step does (sandbox spec §3.9, SB-5b): the attended plan, its own worktree and run directory, and no unconfined fallback.</summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LauncherAttendedSandboxTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-launcher-attended-").FullName;
    private readonly string _sindriHome = Directory.CreateTempSubdirectory("aw-launcher-attended-sindri-").FullName;
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeSessionHost _host = new();
    private readonly FakeSandboxLaunch _sandbox = new();
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose()
    {
        Directory.Delete(_home, recursive: true);
        Directory.Delete(_sindriHome, recursive: true);
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    private Launcher Make(ISandboxLaunch? sandbox) =>
        new(new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode]), _ => _host, new FakeTerminalViewer(), new LauncherOptions(_home) { SindriHome = _sindriHome }, _clock) { Sandbox = sandbox, ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null) });

    private async Task<(LaunchRequest Request, TempGitRepo Repo)> Open()
    {
        var repo = await TempGitRepo.Create();
        _repos.Add(repo);
        return (A.LaunchRequest().InDirectory(repo.Path).Build(), repo);
    }

    [Fact]
    public async Task Attended_open_uses_the_attended_plan()
    {
        var (request, repo) = await Open();

        var result = await Make(_sandbox).Launch(request, TestContext.Current.CancellationToken);

        var started = Assert.IsType<StartedLaunch>(result);
        var prepared = Assert.Single(_sandbox.Prepared);
        Assert.True(prepared.Request.Attended);
        var worktree = Assert.IsType<string>(prepared.Request.Worktree);
        Assert.StartsWith(Path.Combine(repo.Path, ".claude", "worktrees") + Path.DirectorySeparatorChar, worktree, StringComparison.Ordinal);   // never the owner's main checkout
        Assert.True(Directory.Exists(worktree));
        Assert.Equal(worktree, _host.Started[0].WorkDir);
        Assert.True(Directory.Exists(Path.Combine(_sindriHome, "open", request.Launch.Value)));   // the run directory for the plan, denials.json and egress.log
        Assert.Equal("/sessions/home", _host.Started[0].Env["AW_SINDRI_HOME"]);
        Assert.True(started.Sandbox is { Attended: true });   // launch.started carries attended: true
    }

    [Fact]
    public async Task Attended_open_asks_its_host_for_the_attended_status_line()
    {
        var (request, _) = await Open();

        _ = await Make(_sandbox).Launch(request, TestContext.Current.CancellationToken);

        Assert.Equal(SandboxStatusLine.Template(attended: true), _host.Started[0].StatusRight);
    }

    [Fact]
    public async Task Attended_opens_worktree_is_on_its_own_run_branch()
    {
        var (request, repo) = await Open();

        _ = Assert.IsType<StartedLaunch>(await Make(_sandbox).Launch(request, TestContext.Current.CancellationToken));

        var worktree = Assert.IsType<string>(Assert.Single(_sandbox.Prepared).Request.Worktree);
        // why: the sandbox lets the session write refs under sindri/open/<launch> only, so a detached HEAD would leave its commits on no branch (sandbox spec §3.9)
        Assert.Equal($"sindri/open/{request.Launch.Value}/work", (await repo.Git("-C", worktree, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
    }

    [Fact]
    public async Task Attended_open_has_no_unconfined_fallback()
    {
        var (request, _) = await Open();
        _sandbox.Refuse = SindriErrors.LaunchSandboxUnavailable.With("no sandbox tool");

        var result = await Make(_sandbox).Launch(request, TestContext.Current.CancellationToken);

        var failed = Assert.IsType<FailedLaunch>(result);
        Assert.Equal(SindriErrors.LaunchSandboxUnavailable.Code, failed.Error.Code);
        Assert.Empty(_host.Started);   // nothing ran unconfined in its place
        _ = Assert.Single(_sandbox.Released);
    }

    [Fact]
    public async Task Without_a_sandbox_registered_an_attended_open_runs_in_the_checkout_as_before()
    {
        var (request, repo) = await Open();

        var result = await Make(sandbox: null).Launch(request, TestContext.Current.CancellationToken);

        Assert.True(result is StartedLaunch { Sandbox: null });
        Assert.Equal(repo.Path, _host.Started[0].WorkDir);
    }
}
