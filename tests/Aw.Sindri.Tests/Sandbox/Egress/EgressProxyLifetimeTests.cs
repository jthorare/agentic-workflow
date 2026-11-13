using System.Net;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

/// <summary>
/// #3457: when the connection lifetime ends, Handle closes the client itself, whatever step it was on. Before, it returned with the client open and left the
/// close to the listener, so a caller that waited for a reply (the 200 head) waited forever when the lifetime ended first.
/// </summary>
public sealed class EgressProxyLifetimeTests
{
    private static readonly TimeSpan _cap = TimeSpan.FromSeconds(10);

    private static readonly IReadOnlyList<PlanGroup> _groups = [new("model", ["api.anthropic.com"])];

    private static readonly Dictionary<string, IPAddress[]> _dns = new(StringComparer.Ordinal)
    {
        ["api.anthropic.com"] = [IPAddress.Parse("160.79.104.10")],
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_lifetime_that_ends_before_the_head_is_read_closes_the_client()
    {
        var proxy = new EgressProxy(new EgressPolicy(_groups, new FakeEgressResolver(_dns)), new FakeEgressDialer(), new EgressLog(Path.Combine("/nonexistent-egress-dir", "egress.log")),
            new EgressCaps(4, 1024, TimeSpan.FromTicks(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30)));
        var (client, server) = JoinedStream.Pair();
        var handled = proxy.Handle(server, Token);
        await handled.WaitAsync(_cap, Token);

        var read = await client.ReadAsync(new byte[64], Token).AsTask().WaitAsync(_cap, Token);   // a closed client reads 0; an open one waits until the cap

        Assert.Equal(0, read);
    }
}
