using System.Net;
using Aw.Kernel;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Control;

/// <summary>A CLI process pings the control plane once and reuses the answer: every ping is a cold HTTP call (#3727).</summary>
public sealed class ControlPlaneClientPingOnceTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Two_calls_on_one_client_ping_the_control_plane_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var probe = new CountingProbe(FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:1"), 42, 3), new Ping(42, 3)));
        using var client = new ControlPlaneClient(probe, SliceHarness.Json, handler: new EmptyHandler());

        _ = await client.Get<GetStatusResponse>("/sindri/status", ct);
        _ = await client.Get<GetStatusResponse>("/sindri/status", ct);

        Assert.Equal(1, probe.Calls);
    }

    private sealed class CountingProbe(IControlPlaneProbe inner) : IControlPlaneProbe
    {
        public int Calls { get; private set; }

        public ValueTask<Result<(ControlEndpoint Endpoint, Ping Ping)>> Check(CancellationToken cancellationToken)
        {
            Calls++;
            return inner.Check(cancellationToken);
        }
    }

    private sealed class EmptyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }
}
