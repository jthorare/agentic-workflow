using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

public sealed class EgressServiceTests : IDisposable
{
    private readonly Lazy<TempDirectory> _lazy = new(() => new TempDirectory());

    private TempDirectory _temp => _lazy.Value;

    public void Dispose()
    {
        if (_lazy.IsValueCreated)
        {
            _lazy.Value.Dispose();
        }
    }

    private static PlanNetwork Network(string dir) => new(NetworkMode.Enforce, [], 7480, 3128, dir);

    private EgressService Service(Func<string, string?> env) =>
        new(new FakeEgressResolver(new Dictionary<string, System.Net.IPAddress[]>(StringComparer.Ordinal)), new FakeEgressDialer(), env, Path.Combine(_temp.Path, "run"));

    private string FakeSocat()
    {
        var socat = Path.Combine(_temp.Path, "socat");
        File.WriteAllText(socat, "#!/bin/sh\n");
        return socat;
    }

    [Fact, Trait("Lane", "unit")]
    public void Missing_socat_is_014_no_forwarder()
    {
        var result = Service(n => n == SandboxEnvironment.Socat ? "/nonexistent/socat" : null).Start(Network("/tmp/sindri-x/a1b2c3d4"), EgressCaps.Default);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.LaunchSandboxUnavailable.Code, result.Error.Code);
        Assert.Equal(SandboxReasons.NoForwarder, SandboxRefusal.ReasonOf(result.Error));
    }

    [Fact, Trait("Lane", "integration")]
    public void A_socket_directory_that_does_not_fit_is_016_net_forward()
    {
        var socat = FakeSocat();

        var result = Service(n => n == SandboxEnvironment.Socat ? socat : null).Start(Network("/tmp/" + new string('d', 100)), EgressCaps.Default);

        Assert.Equal(SindriErrors.LaunchSelfCheckFailed.Code, result.Error.Code);
        Assert.Equal(SandboxReasons.NetForward, SandboxRefusal.ReasonOf(result.Error));
    }

    [Fact, Trait("Lane", "integration")]
    public void A_directory_that_cannot_be_created_is_016_net_forward_not_an_exception()
    {
        var socat = FakeSocat();
        var blocker = Path.Combine(_temp.Path, "blocker");
        File.WriteAllText(blocker, string.Empty);

        var result = Service(n => n == SandboxEnvironment.Socat ? socat : null).Start(Network(Path.Combine(blocker, "n")), EgressCaps.Default);

        Assert.Equal(SindriErrors.LaunchSelfCheckFailed.Code, result.Error.Code);
        Assert.Equal(SandboxReasons.NetForward, SandboxRefusal.ReasonOf(result.Error));
    }

    [Fact, Trait("Lane", "integration")]
    public void A_bind_failure_is_016_net_forward_and_leaves_no_directory()
    {
        var socat = FakeSocat();
        var net = Path.Combine(_temp.Path, "n");
        _ = Directory.CreateDirectory(net);
        File.WriteAllText(Path.Combine(net, "sindri.sock"), "in the way");

        var result = Service(n => n == SandboxEnvironment.Socat ? socat : null).Start(Network(net), EgressCaps.Default);

        Assert.Equal(SandboxReasons.NetForward, SandboxRefusal.ReasonOf(result.Error));
        Assert.False(Directory.Exists(net));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Dispose_twice_and_after_the_directory_is_gone_are_no_ops()
    {
        var socat = FakeSocat();
        var net = Path.Combine(_temp.Path, "n");
        var started = Service(n => n == SandboxEnvironment.Socat ? socat : null).Start(Network(net), EgressCaps.Default).Value;

        await started.DisposeAsync();
        await started.DisposeAsync();
        Assert.False(Directory.Exists(net));

        var again = Service(n => n == SandboxEnvironment.Socat ? socat : null).Start(Network(net), EgressCaps.Default).Value;
        Directory.Delete(net, recursive: true);
        await again.DisposeAsync();
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Log_is_flushed_on_a_timer_while_a_tunnel_is_open()
    {
        var socat = FakeSocat();
        var service = new EgressService(
            new FakeEgressResolver(new Dictionary<string, System.Net.IPAddress[]>(StringComparer.Ordinal) { ["api.anthropic.com"] = [System.Net.IPAddress.Parse("160.79.104.10")] }),
            new FakeEgressDialer(),
            n => n == SandboxEnvironment.Socat ? socat : null,
            Path.Combine(_temp.Path, "run"));
        var group = new PlanGroup("model", ["api.anthropic.com"]);
        await using var running = service.Start(new PlanNetwork(NetworkMode.Enforce, [group], 7480, 3128, Path.Combine(_temp.Path, "n")), EgressCaps.Default).Value;
        using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
        await socket.ConnectAsync(new System.Net.Sockets.UnixDomainSocketEndPoint(Path.Combine(_temp.Path, "n", "proxy.sock")), TestContext.Current.CancellationToken);
        _ = await socket.SendAsync("CONNECT api.anthropic.com:443 HTTP/1.1\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken);

        var line = string.Empty;
        for (var i = 0; i < 50 && line.Length == 0; i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            line = File.Exists(Path.Combine(_temp.Path, "run", "egress.log")) ? string.Concat(await File.ReadAllLinesAsync(Path.Combine(_temp.Path, "run", "egress.log"), TestContext.Current.CancellationToken)) : string.Empty;
        }

        Assert.Equal("api.anthropic.com\t443\tallowed\t1", line);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_failing_first_flush_is_016_and_leaves_nothing_listening()
    {
        var socat = FakeSocat();
        var run = Path.Combine(_temp.Path, "run");
        _ = Directory.CreateDirectory(Path.Combine(run, "egress.log.tmp"));
        var net = Path.Combine(_temp.Path, "n");

        var result = Service(n => n == SandboxEnvironment.Socat ? socat : null).Start(Network(net), EgressCaps.Default);

        Assert.Equal(SandboxReasons.NetForward, SandboxRefusal.ReasonOf(result.Error));
        Assert.False(Directory.Exists(net));
        using var probe = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
        _ = await Assert.ThrowsAnyAsync<System.Net.Sockets.SocketException>(async () => await probe.ConnectAsync(new System.Net.Sockets.UnixDomainSocketEndPoint(Path.Combine(net, "sindri.sock")), TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_flush_loop_survives_a_failed_tick_warns_and_writes_on_the_next()
    {
        var socat = FakeSocat();
        var warnings = new List<string>();
        var run = Path.Combine(_temp.Path, "run");
        var service = new EgressService(new FakeEgressResolver(new Dictionary<string, System.Net.IPAddress[]>(StringComparer.Ordinal)), new FakeEgressDialer(), n => n == SandboxEnvironment.Socat ? socat : null, run, warnings.Add);
        await using var running = service.Start(Network(Path.Combine(_temp.Path, "n")), EgressCaps.Default).Value;
        var blocker = Path.Combine(run, "egress.log.tmp");
        _ = Directory.CreateDirectory(blocker);
        running.Log.Record("a.example", 443, EgressVerdict.Denied);

        await Task.Delay(1500, TestContext.Current.CancellationToken);
        Directory.Delete(blocker);
        var line = string.Empty;
        for (var i = 0; i < 40 && line.Length == 0; i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            line = string.Concat(await File.ReadAllLinesAsync(Path.Combine(run, "egress.log"), TestContext.Current.CancellationToken));
        }

        Assert.Equal("a.example\t443\tdenied\t1", line);
        Assert.NotEmpty(warnings);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Dispose_does_not_throw_when_the_final_flush_cannot_write()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "unix modes");
        var socat = FakeSocat();
        var run = Path.Combine(_temp.Path, "run");
        var running = Service(n => n == SandboxEnvironment.Socat ? socat : null).Start(Network(Path.Combine(_temp.Path, "n")), EgressCaps.Default).Value;
        running.Log.Record("a.example", 443, EgressVerdict.Denied);
        Directory.CreateDirectory(Path.Combine(run, "egress.log.tmp"));

        await running.DisposeAsync();
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_flush_that_keeps_failing_warns_once_not_every_tick()
    {
        var socat = FakeSocat();
        var warnings = new List<string>();
        var run = Path.Combine(_temp.Path, "run");
        var service = new EgressService(new FakeEgressResolver(new Dictionary<string, System.Net.IPAddress[]>(StringComparer.Ordinal)), new FakeEgressDialer(), n => n == SandboxEnvironment.Socat ? socat : null, run, warnings.Add);
        await using var running = service.Start(Network(Path.Combine(_temp.Path, "n")), EgressCaps.Default).Value;
        var blocker = Path.Combine(run, "egress.log.tmp");
        _ = Directory.CreateDirectory(blocker);
        running.Log.Record("a.example", 443, EgressVerdict.Denied);

        await Task.Delay(3500, TestContext.Current.CancellationToken);

        _ = Assert.Single(warnings);
        Directory.Delete(blocker);
    }
}
