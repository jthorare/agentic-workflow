using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// What only the real adapter can get wrong: a server that hangs, and the caller's own cancellation.
[Lane(TestLane.Integration)]
public sealed class HttpOnyxHealthTests : IDisposable
{
    private readonly LoopbackFileServer _server = new(new Dictionary<string, byte[]>(StringComparer.Ordinal) { [OnyxRoutes.Health] = "ok"u8.ToArray(), [OnyxRoutes.Version] = "{\"backend_version\":\"v4.9.0\"}"u8.ToArray() });
    private readonly HttpClient _client = new();

    public void Dispose()
    {
        _server.Dispose();
        _client.Dispose();
    }

    [Fact]
    public async Task A_server_that_hangs_is_not_healthy_after_the_request_timeout()
    {
        _server.Delay = TimeSpan.FromSeconds(3);

        var probe = await new HttpOnyxHealth(_client, TimeSpan.FromMilliseconds(200)).Probe(_server.BaseUri.Port, TestContext.Current.CancellationToken);

        Assert.Equal(new OnyxProbe(Healthy: false, BackendVersion: null), probe);
    }

    [Fact]
    public async Task The_callers_cancellation_is_thrown_not_swallowed()
    {
        using var source = new CancellationTokenSource();
        _server.Delay = TimeSpan.FromSeconds(3);
        source.CancelAfter(TimeSpan.FromMilliseconds(200));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await new HttpOnyxHealth(_client).Probe(_server.BaseUri.Port, source.Token));
    }

    [Fact]
    public async Task A_redirect_is_not_followed_so_the_probe_never_leaves_the_port_it_was_given()
    {
        using var elsewhere = new LoopbackFileServer(new Dictionary<string, byte[]>(StringComparer.Ordinal) { [OnyxRoutes.Health] = "ok"u8.ToArray(), [OnyxRoutes.Version] = "{\"backend_version\":\"v4.9.0\"}"u8.ToArray() });
        _server.Redirects[OnyxRoutes.Health] = new Uri(elsewhere.BaseUri, OnyxRoutes.Health).AbsoluteUri;
        using var client = HttpOnyxHealth.CreateClient();

        var probe = await new HttpOnyxHealth(client).Probe(_server.BaseUri.Port, TestContext.Current.CancellationToken);

        Assert.Equal(new OnyxProbe(Healthy: false, BackendVersion: null), probe);
        Assert.Empty(elsewhere.Requests);
    }

    [Fact]
    public async Task The_health_route_is_asked_before_the_version_route()
    {
        _ = await new HttpOnyxHealth(_client).Probe(_server.BaseUri.Port, TestContext.Current.CancellationToken);

        Assert.Equal([OnyxRoutes.Health, OnyxRoutes.Version], _server.Requests);
    }

    [Fact]
    public async Task The_version_route_is_not_asked_while_health_fails()
    {
        _server.Statuses[OnyxRoutes.Health] = 503;

        _ = await new HttpOnyxHealth(_client).Probe(_server.BaseUri.Port, TestContext.Current.CancellationToken);

        Assert.Equal([OnyxRoutes.Health], _server.Requests);
    }
}
