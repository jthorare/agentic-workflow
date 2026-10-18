using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

public sealed class EgressInitTests
{
    private static readonly PlanNetwork _network = new(NetworkMode.Enforce, [], 7480, 3128, "/run/user/1000/sindri/a1b2c3d4");

    [Fact, Trait("Lane", "unit")]
    public void Init_starts_both_forwards_on_their_ports_and_closes_fds_3_to_9()
    {
        var script = EgressInit.Render(_network, "socat", "/run/sindri/run/socat.log");

        Assert.Contains("exec 3>&- 4>&- 5>&- 6>&- 7>&- 8>&- 9>&-", script, StringComparison.Ordinal);
        Assert.Contains("'socat' TCP-LISTEN:3128,bind=127.0.0.1,fork,reuseaddr UNIX-CONNECT:/run/sindri/net/proxy.sock >>'/run/sindri/run/socat.log' 2>&1 &", script, StringComparison.Ordinal);
        Assert.Contains("'socat' TCP-LISTEN:7480,bind=127.0.0.1,fork,reuseaddr UNIX-CONNECT:/run/sindri/net/sindri.sock >>'/run/sindri/run/socat.log' 2>&1 &", script, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void NO_PROXY_names_loopback_so_the_sindri_port_skips_the_proxy()
    {
        var script = EgressInit.Render(_network, "socat", "/dev/null");

        Assert.Contains("export NO_PROXY='127.0.0.1,localhost,::1'", script, StringComparison.Ordinal);
        Assert.Equal("127.0.0.1,localhost,::1", EgressInit.NoProxy);
    }

    [Fact, Trait("Lane", "unit")]
    public void Init_forwards_signals_to_the_agent_and_stops_the_forwarders_on_exit()
    {
        var script = EgressInit.Render(_network, "socat", "/dev/null");

        Assert.Contains("trap 'kill \"$child\" 2>/dev/null' TERM INT HUP", script, StringComparison.Ordinal);
        Assert.Contains("trap 'kill \"$proxy\" \"$sindri\" 2>/dev/null' EXIT", script, StringComparison.Ordinal);
        Assert.Contains("\"$@\" <&3 3<&- &", script, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_forwarder_that_died_at_start_is_016_net_forward()
    {
        var error = EgressInit.ForwarderFailed("socat exited");

        Assert.Equal(SindriErrors.LaunchSelfCheckFailed.Code, error.Code);
        Assert.Equal(SandboxReasons.NetForward, SandboxRefusal.ReasonOf(error));
        Assert.Equal(70, EgressInit.ForwarderExitCode);
    }
}
