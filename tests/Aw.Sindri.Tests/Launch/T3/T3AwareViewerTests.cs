using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch.T3;

/// <summary>The launcher's viewer sends a T3 session to the T3 app and everything else to the terminal.</summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class T3AwareViewerTests : IDisposable
{
    private static readonly TimeSpan _window = TimeSpan.FromSeconds(3);
    private readonly T3Rig _rig = new();
    private readonly FakeTerminalViewer _terminal = new();

    public void Dispose() => _rig.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_tmux_session_opens_in_the_terminal()
    {
        var viewer = new T3AwareViewer(_terminal, _rig.Host());

        _ = await viewer.Open(new ViewerRequest(new HostRef(HostKind.Tmux, "snd-one"), ["tmux", "attach"], "sindri ITM-1", _window), Token);

        _ = Assert.Single(_terminal.Requests);
    }

    [Fact]
    public async Task A_T3_session_opens_its_link_without_a_terminal()
    {
        var host = _rig.Host();
        var started = await host.Start(_rig.Session("snd-one"), _window, Token);

        var shown = await new T3AwareViewer(_terminal, host).Open(new ViewerRequest(started.Value, host.AttachArgv(started.Value), "sindri ITM-1", _window), Token);

        Expect.Equal(ViewerOutcome.Opened, shown);
        Expect.Equal(0, _terminal.Requests.Count);
    }

    [Fact]
    public async Task An_attach_that_only_knows_the_name_finds_the_T3_session()
    {
        var host = _rig.Host();
        var started = await host.Start(_rig.Session("snd-one"), _window, Token);

        var shown = await new T3AwareViewer(_terminal, host).Open(new ViewerRequest(new HostRef(HostKind.Tmux, "snd-one"), host.AttachArgv(started.Value), "sindri ITM-1", _window), Token);

        Expect.Equal(ViewerOutcome.Opened, shown);
        Expect.Equal(0, _terminal.Requests.Count);
    }

    [Fact]
    public async Task An_opener_that_fails_is_a_viewer_failure()
    {
        var options = _rig.Options with { Opener = "false" };
        var host = new T3SessionHost(_rig.Api, new T3SessionMap(options), options, _rig.Repos);
        var started = await host.Start(_rig.Session("snd-one"), _window, Token);

        var shown = await new T3AwareViewer(_terminal, host).Open(new ViewerRequest(started.Value, host.AttachArgv(started.Value), "sindri ITM-1", _window), Token);

        Expect.Equal(ViewerOutcomeKind.Failed, shown.Kind);
    }
}
