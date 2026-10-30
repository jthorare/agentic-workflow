using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>#1765: the launch script holds a session's env, so its directory never outlives the start.</summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class TmuxSessionHostLaunchScriptTests : IDisposable
{
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(4);
    private readonly PrivateTmux _tmux = new();

    public void Dispose() => _tmux.Dispose();

    [Fact]
    public async Task A_started_session_deletes_its_launch_script()
    {
        var host = new TmuxSessionHost(_tmux.Options);

        var started = await host.Start(Session("snd-script", new Dictionary<string, string>(StringComparer.Ordinal) { ["SND_SECRET"] = "x" }), _deadline, CancellationToken.None);

        Expect.True(started.IsSuccess);
        Expect.Equal(0, await LaunchDirsWhenGone());
    }

    [Fact]
    public async Task A_failed_start_leaves_no_launch_script_behind()
    {
        var host = new TmuxSessionHost(_tmux.Options);
        var first = await host.Start(Session("snd-dup", new Dictionary<string, string>(StringComparer.Ordinal)), _deadline, CancellationToken.None);

        var second = await host.Start(Session("snd-dup", new Dictionary<string, string>(StringComparer.Ordinal) { ["SND_SECRET"] = "x" }), _deadline, CancellationToken.None);

        Expect.True(first.IsSuccess);
        Expect.True(second.IsError && second.Error.Code == KernelErrors.Conflict.Code);
        Expect.Equal(0, await LaunchDirsWhenGone());   // the first script ran and removed its own directory; the second was removed by the host
    }

    private HostSession Session(string name, Dictionary<string, string> env) => new(name, _tmux.Scratch, ["sleep", "60"], env);

    private async Task<int> LaunchDirsWhenGone()
    {
        var count = 0;
        for (var i = 0; i < 40; i++)
        {
            count = Directory.Exists(_tmux.TmuxTmpDir) ? Directory.GetDirectories(_tmux.TmuxTmpDir, "snd-*").Length : 0;
            if (count == 0)
            {
                return 0;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return count;
    }
}
