using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The launcher's tmux server is its own: aw-meta teams run on a socket named after the team ('sindri' today).</summary>
[Lane(TestLane.Unit)]
public sealed class TmuxHostDefaultSocketTests
{
    [Fact]
    public void Default_socket_is_never_the_team_socket()
    {
        var host = new TmuxSessionHost(new TmuxHostOptions());

        Expect.Equal("sindri-launch", new TmuxHostOptions().Socket);
        Expect.True(new TmuxHostOptions().Socket != "sindri");
        Expect.True(new TmuxHostOptions().Socket.EndsWith("-launch", StringComparison.Ordinal));   // a bare team name never ends this way
        Expect.Sequence(["tmux", "-L", "sindri-launch", "attach", "-t", "=snd-one"], host.AttachArgv(new HostRef(HostKind.Tmux, "snd-one")));
    }
}
