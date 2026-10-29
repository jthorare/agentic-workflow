using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>A sandboxed session's tmux status line, on a real tmux of a private socket (sandbox spec §7): set on that session only, with the prefix of the same server.</summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class TmuxSessionHostStatusLineTests : IDisposable
{
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(4);
    private readonly PrivateTmux _tmux = new();

    public void Dispose() => _tmux.Dispose();

    private HostSession Session(string name, string? statusRight) =>
        new(name, _tmux.Scratch, ["sleep", "60"], new Dictionary<string, string>(StringComparer.Ordinal)) { StatusRight = statusRight };

    private string? StatusRight(string session) => _tmux.Run("show-options", "-t", "=" + session + ":", "-v", "status-right")?.Trim();

    [Fact]
    public async Task A_sandboxed_step_gets_the_golden_with_the_default_prefix()
    {
        var started = await new TmuxSessionHost(_tmux.Options).Start(Session("snd-step", SandboxStatusLine.Template(attended: false)), _deadline, CancellationToken.None);

        Expect.True(started.IsSuccess);
        Expect.Equal("Sandboxed: push and secrets blocked. Outside it: Ctrl-b c", StatusRight("snd-step"));
    }

    [Fact]
    public async Task An_attended_session_gets_the_attended_golden()
    {
        _ = await new TmuxSessionHost(_tmux.Options).Start(Session("snd-open", SandboxStatusLine.Template(attended: true)), _deadline, CancellationToken.None);

        Expect.Equal("Sandboxed (attended): push and secrets blocked. Outside it: Ctrl-b c", StatusRight("snd-open"));
    }

    [Fact]
    public async Task A_remapped_prefix_of_the_same_server_is_shown()
    {
        _ = _tmux.Run("new-session", "-d", "-s", "other", "sleep", "60");
        _ = _tmux.Run("set-option", "-g", "prefix", "C-a");

        _ = await new TmuxSessionHost(_tmux.Options).Start(Session("snd-remap", SandboxStatusLine.Template(attended: false)), _deadline, CancellationToken.None);

        Expect.Equal("Sandboxed: push and secrets blocked. Outside it: Ctrl-a c", StatusRight("snd-remap"));
    }

    [Fact]
    public async Task The_whole_line_fits_the_status_area()
    {
        _ = await new TmuxSessionHost(_tmux.Options).Start(Session("snd-wide", SandboxStatusLine.Template(attended: true)), _deadline, CancellationToken.None);

        var length = int.Parse(_tmux.Run("show-options", "-t", "=snd-wide:", "-v", "status-right-length")!.Trim(), System.Globalization.CultureInfo.InvariantCulture);
        Expect.True(length >= SandboxStatusLine.Render(attended: true, "C-b").Length);
    }

    [Fact]
    public async Task Other_sessions_and_the_server_default_are_left_alone()
    {
        _ = _tmux.Run("new-session", "-d", "-s", "plain", "sleep", "60");

        _ = await new TmuxSessionHost(_tmux.Options).Start(Session("snd-only", SandboxStatusLine.Template(attended: false)), _deadline, CancellationToken.None);

        Expect.True(!StatusRight("plain")!.Contains("Sandboxed", StringComparison.Ordinal));
        Expect.True(!_tmux.Run("show-options", "-g", "-v", "status-right")!.Contains("Sandboxed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_session_with_no_status_line_keeps_the_default()
    {
        _ = await new TmuxSessionHost(_tmux.Options).Start(Session("snd-plain", statusRight: null), _deadline, CancellationToken.None);

        Expect.True(!StatusRight("snd-plain")!.Contains("Sandboxed", StringComparison.Ordinal));
    }
}
