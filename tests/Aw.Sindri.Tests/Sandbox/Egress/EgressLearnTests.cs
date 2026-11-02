using System.Net;
using System.Text;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Egress;

/// <summary>Learn mode in the proxy (sandbox spec §3.2): unlisted public hosts on 80/443 pass and are logged 'learned', until the expiry.</summary>
public sealed class EgressLearnTests
{
    private static readonly IReadOnlyList<PlanGroup> _groups = [new("model", ["api.anthropic.com"])];

    private static readonly Dictionary<string, IPAddress[]> _dns = new(StringComparer.Ordinal)
    {
        ["api.anthropic.com"] = [IPAddress.Parse("160.79.104.10")],
        ["new.example"] = [IPAddress.Parse("93.184.216.34")],
        ["inside.example"] = [IPAddress.Parse("10.0.0.7")],
    };

    private static readonly DateTimeOffset _until = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private static EgressPolicy Policy(FakeTimeProvider clock) => new(_groups, new FakeEgressResolver(_dns), NetworkMode.Learn, _until, clock);

    private static FakeTimeProvider ClockAt(DateTimeOffset at)
    {
        var clock = new FakeTimeProvider();
        clock.SetUtcNow(at);
        return clock;
    }

    private static async Task<string> ReadHead(Stream stream)
    {
        var text = new StringBuilder();
        var one = new byte[1];
        while (!text.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal) && await stream.ReadAsync(one, TestContext.Current.CancellationToken) > 0)
        {
            _ = text.Append((char)one[0]);
        }

        return text.ToString();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Learn_mode_lets_an_unlisted_host_through_and_logs_learned()
    {
        var dialer = new FakeEgressDialer();
        var log = new EgressLog(Path.Combine("/nonexistent-egress-dir", "egress.log"));
        var proxy = new EgressProxy(Policy(ClockAt(_until.AddDays(-1))), dialer, log, EgressCaps.Default);
        var (client, server) = JoinedStream.Pair();
        var handled = proxy.Handle(server, TestContext.Current.CancellationToken);

        await client.WriteAsync("CONNECT new.example:443 HTTP/1.1\r\nHost: new.example:443\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken);
        Assert.StartsWith("HTTP/1.1 200", await ReadHead(client), StringComparison.Ordinal);
        var upstream = await dialer.Upstream.Task;
        await client.DisposeAsync();
        // the upstream plays the server: it closes once the proxy has half-closed it, else the relay waits out EgressCaps.HalfCloseGrace (30 s)
        await upstream.DisposeAsync();
        await handled;

        Assert.Equal([(IPAddress.Parse("93.184.216.34"), 443)], dialer.Dialed);
        Assert.Equal(1, log.Count("new.example", 443, EgressVerdict.Learned));
        Assert.Equal(0, log.Count("new.example", 443, EgressVerdict.Allowed));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Learn_mode_still_refuses_private_addresses_and_other_ports()
    {
        var policy = Policy(ClockAt(_until.AddDays(-1)));

        var privateAddress = await policy.Decide("inside.example", 443, TestContext.Current.CancellationToken);
        var literal = await policy.Decide("192.168.1.5", 80, TestContext.Current.CancellationToken);
        var otherPort = await policy.Decide("new.example", 22, TestContext.Current.CancellationToken);

        Assert.Equal(EgressVerdict.Refused, privateAddress.Verdict);
        Assert.Equal(EgressVerdict.Refused, literal.Verdict);
        Assert.Equal(EgressVerdict.Denied, otherPort.Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Learn_mode_ends_at_its_expiry_without_a_restart()
    {
        var clock = ClockAt(_until.AddSeconds(-1));
        var policy = Policy(clock);

        var before = await policy.Decide("new.example", 443, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(1));
        var atExpiry = await policy.Decide("new.example", 443, TestContext.Current.CancellationToken);
        var listed = await policy.Decide("api.anthropic.com", 443, TestContext.Current.CancellationToken);

        Assert.Equal(EgressVerdict.Learned, before.Verdict);
        Assert.Equal(EgressVerdict.Denied, atExpiry.Verdict);
        Assert.Equal(EgressVerdict.Allowed, listed.Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Enforce_mode_ignores_a_learn_expiry()
    {
        var policy = new EgressPolicy(_groups, new FakeEgressResolver(_dns), NetworkMode.Enforce, _until, ClockAt(_until.AddDays(-1)));

        var decision = await policy.Decide("new.example", 443, TestContext.Current.CancellationToken);

        Assert.Equal(EgressVerdict.Denied, decision.Verdict);
    }
}
