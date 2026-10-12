using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Control;

/// <summary>The HTTP probe against a minimal loopback server, plus the failures only a real socket has: a stale record and a silent listener.</summary>
[Lane(TestLane.Integration)]
public sealed class HttpControlPlaneProbeContractTests : ControlPlaneProbeContract, IAsyncDisposable
{
    private readonly InMemoryEndpointFile _endpoints = new();
    private readonly List<IAsyncDisposable> _servers = [];
    private readonly List<IDisposable> _probes = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        _probes.ForEach(p => p.Dispose());
        foreach (var server in _servers)
        {
            await server.DisposeAsync();
        }
    }

    protected override async ValueTask<IControlPlaneProbe> Create((int Pid, long Epoch)? recorded, Ping answer)
    {
        var server = new PingServer(answer);
        _servers.Add(server);
        if (recorded is { } r)
        {
            _ = await _endpoints.Write(new ControlEndpoint(server.Url, r.Pid, r.Epoch), Token);
        }

        return Probe();
    }

    [Fact(Timeout = 10_000)]
    public async Task A_stale_record_whose_port_is_closed_is_not_running()
    {
        var ct = TestContext.Current.CancellationToken;
        var server = new PingServer(new Ping(42, 3));
        var url = server.Url;
        await server.DisposeAsync();
        _ = await _endpoints.Write(new ControlEndpoint(url, 42, 3), ct);

        Assert.Equal(SindriErrors.NotRunning.Code, (await Probe().Check(ct)).Error.Code);
    }

    [Fact(Timeout = 10_000)]
    public async Task A_listener_that_never_answers_is_a_timeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var server = new PingServer(answer: null);
        _servers.Add(server);
        _ = await _endpoints.Write(new ControlEndpoint(server.Url, 42, 3), ct);

        Assert.Equal(SindriErrors.ApiTimeout.Code, (await Probe().Check(ct)).Error.Code);
    }

    private HttpControlPlaneProbe Probe()
    {
        var probe = new HttpControlPlaneProbe(_endpoints, SliceHarness.Json);
        _probes.Add(probe);
        return probe;
    }
}
