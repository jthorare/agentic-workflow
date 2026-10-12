using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Control;

/// <summary>The CLI's client against a control plane that is recorded and pings fine but never answers the call itself.</summary>
public sealed class ControlPlaneClientTests
{
    [Fact(Timeout = 15_000), Trait("Lane", "integration")]
    public async Task Status_from_a_control_plane_that_stops_answering_is_SND_API_002_exit_2()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var silent = new PingServer(answer: null);
        var endpoint = new ControlEndpoint(silent.Url, 42, 3);
        var probe = FakeControlPlaneProbe.For(endpoint, new Ping(42, 3));
        using var client = new ControlPlaneClient(probe, SliceHarness.Json, TimeSpan.FromSeconds(1));
        var stderr = new StringWriter();
        var verbs = new Verbs(client, probe, new Output(new StringWriter(), stderr, SliceHarness.Json), new ServiceCollection().BuildServiceProvider());

        var code = await verbs.Status(json: false, ct);

        Assert.Equal(2, code);
        Assert.StartsWith($"SND-API-002 {silent.Url.Authority} didn't answer /sindri/status within 1 s", stderr.ToString(), StringComparison.Ordinal);
    }
}
