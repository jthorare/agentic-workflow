using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.Viewers;

/// <summary>Four viewer scripts against a real tmux on a private socket (launcher spec §8.3): no display is ever needed, the scripts are the terminal.</summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ViewerIntegrationTests : IDisposable
{
    // $1 title; $2.. tmux -L <sock> attach-session -t =<name>: a control-mode client is a tmux client without a terminal.
    private const string Attach = "sleep 20 | \"$2\" \"$3\" \"$4\" -C \"$5\" \"$6\" \"$7\" >/dev/null";

    private readonly PrivateTmux _tmux = new();
    private readonly TmuxSessionHost _host;
    private readonly HostRef _session = new(HostKind.Tmux, "snd-view");

    public ViewerIntegrationTests()
    {
        _host = new TmuxSessionHost(_tmux.Options);
    }

    public void Dispose() => _tmux.Dispose();

    private async Task<ViewerRequest> Started()
    {
        var started = await _host.Start(new HostSession(_session.Name, _tmux.Scratch, ["sleep", "60"], new Dictionary<string, string>(StringComparer.Ordinal)), TimeSpan.FromSeconds(4), CancellationToken.None);
        Expect.True(started.IsSuccess);
        return new ViewerRequest(_session, _host.AttachArgv(_session), "sindri ITM-1", TimeSpan.FromSeconds(1.5)) { Cwd = _tmux.Scratch };
    }

    private ExecTemplateViewer Viewer(string body)
    {
        var path = Path.Combine(_tmux.Scratch, "viewer.sh");
        File.WriteAllText(path, "#!/bin/sh\n" + body + "\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["TMUX_TMPDIR"] = _tmux.TmuxTmpDir };
        return new ExecTemplateViewer(new ExecTemplateOptions("stub", [path, "{title}", "{cmd...}"]) { Environment = env }, _host, TimeProvider.System);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_viewer_that_attaches_is_opened()
    {
        var request = await Started();

        var outcome = await Viewer(Attach).Open(request, TestContext.Current.CancellationToken);

        Expect.Equal(ViewerOutcome.Opened, outcome);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_viewer_that_attaches_late_is_unconfirmed_and_the_client_arrives_after()
    {
        var request = await Started();

        var outcome = await Viewer("sleep 2.5\n" + Attach).Open(request, TestContext.Current.CancellationToken);

        Expect.Equal(ViewerOutcome.Unconfirmed, outcome);
        var late = false;
        for (var i = 0; i < 40 && !late; i++)
        {
            late = await _host.AttachedClients(_session, CancellationToken.None) > 0;
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Expect.True(late);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_viewer_that_exits_0_without_attaching_is_unconfirmed()
    {
        var request = await Started();

        var outcome = await Viewer("exit 0").Open(request, TestContext.Current.CancellationToken);

        Expect.Equal(ViewerOutcome.Unconfirmed, outcome);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_viewer_that_fails_is_failed_with_its_stderr_and_the_session_stays_attachable()
    {
        var request = await Started();

        var outcome = await Viewer("echo 'cannot open display' >&2; exit 1").Open(request, TestContext.Current.CancellationToken);

        Expect.Equal(ViewerOutcomeKind.Failed, outcome.Kind);
        Expect.Equal(SindriErrors.LaunchViewerFailed.Code, outcome.Code);
        Expect.True(outcome.Detail!.Contains("cannot open display", StringComparison.Ordinal));
        Expect.True(_tmux.HasSession(_session.Name));
    }
}
