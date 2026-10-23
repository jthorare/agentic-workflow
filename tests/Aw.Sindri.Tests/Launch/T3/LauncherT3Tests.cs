using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch.T3;

/// <summary>The launcher with the t3 host: a human open becomes a T3 thread with the item's seed, shown in the app; a stop interrupts it; Steps stay on tmux.</summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LauncherT3Tests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-launcher-t3-").FullName;
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeSessionHost _tmux = new();
    private readonly FakeTerminalViewer _terminal = new();
    private readonly T3Rig _rig = new();

    public void Dispose()
    {
        _rig.Dispose();
        Directory.Delete(_home, recursive: true);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Launcher Make(T3SessionHost t3) =>
        new(new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode]), attended => attended ? t3 : _tmux, new T3AwareViewer(_terminal, t3), new LauncherOptions(_home), _clock)
        {
            ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null),
            OtherHosts = [t3],
        });

    [Fact]
    public async Task A_human_open_starts_a_T3_thread_with_the_items_seed_and_opens_it_in_the_app()
    {
        _rig.Api.AddProject("/work/repo");
        var t3 = _rig.Host();

        var result = await Make(t3).Launch(A.LaunchRequest().Build(), Token);

        var started = Assert.IsType<StartedLaunch>(result);
        Expect.Equal((HostKind.T3, ViewerOutcomeKind.Opened), (started.Host.Kind, started.Viewer.Kind));
        Expect.Sequence(["true", "t3code://threads/env-1/thread-1"], started.Attach);
        var launched = Assert.Single(_rig.Api.Launched);
        Expect.True(launched.Message.StartsWith("Sindri item " + A.FirstItemId.Value, StringComparison.Ordinal));
        Expect.Equal("sindri " + A.FirstItemId.Value, launched.Title);
        Expect.Equal(new T3Model("claudeAgent", "claude-test"), launched.Model);
        Expect.True(launched.WorktreePath is null);
        Expect.Equal(0, _tmux.Started.Count);
    }

    [Fact]
    public async Task Stop_interrupts_the_T3_thread()
    {
        _rig.Api.AddProject("/work/repo");
        var launcher = Make(_rig.Host());
        _ = await launcher.Launch(A.LaunchRequest().Build(), Token);

        var stopped = await launcher.Stop(A.FirstLaunchId, Token);

        Expect.True(stopped.IsSuccess);
        Expect.Equal("thread-1", Assert.Single(_rig.Api.Interrupted));
    }

    [Fact]
    public async Task A_step_stays_on_tmux()
    {
        var result = await Make(_rig.Host()).Launch(A.LaunchRequest().Unattended(new StepArgs(Model: null, Policy: null, new StepRef("build", "agent"))).Build(), Token);

        Expect.True(result is StartedLaunch { Host.Kind: HostKind.Tmux });
        Expect.Equal(0, _rig.Api.Launched.Count);
    }
}
