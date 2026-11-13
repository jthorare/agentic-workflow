using System.Net;
using System.Text;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

/// <summary>
/// The connection cap's refusal (#3484): a 503 write that throws past Reply (ObjectDisposedException, or the run's cancellation) must not keep a connection slot, or the proxy refuses every later connection for the rest of the run.
/// </summary>
public sealed class EgressProxyCapTests
{
    private static readonly IReadOnlyList<PlanGroup> _groups = [new("model", ["api.anthropic.com"])];

    private static readonly Dictionary<string, IPAddress[]> _dns = new(StringComparer.Ordinal) { ["api.anthropic.com"] = [IPAddress.Parse("160.79.104.10")] };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_client_closed_during_the_503_gives_its_slot_back()
    {
        var dialer = new FakeEgressDialer();
        var proxy = new EgressProxy(new EgressPolicy(_groups, new FakeEgressResolver(_dns)), dialer, new EgressLog(Path.Combine("/nonexistent-egress-dir", "egress.log")),
            new EgressCaps(1, 1024 * 1024, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(200)));
        var (first, firstServer) = JoinedStream.Pair();
        var firstHandled = proxy.Handle(firstServer, Token);
        await first.WriteAsync("CONNECT api.anthropic.com:443 HTTP/1.1\r\n\r\n"u8.ToArray(), Token);
        _ = await ReadHead(first);

        var refused = await Record.ExceptionAsync(() => proxy.Handle(new ClosedStream(), Token));   // the cap is full: this one gets the 503, and its write throws past Reply
        await first.DisposeAsync();
        await firstHandled.WaitAsync(TimeSpan.FromSeconds(10), Token);

        var (third, thirdServer) = JoinedStream.Pair();
        var thirdHandled = proxy.Handle(thirdServer, Token);
        await third.WriteAsync("CONNECT api.anthropic.com:443 HTTP/1.1\r\n\r\n"u8.ToArray(), Token);
        var head = await ReadHead(third);
        await third.DisposeAsync();
        await thirdHandled.WaitAsync(TimeSpan.FromSeconds(10), Token);

        Assert.Null(refused);
        Assert.StartsWith("HTTP/1.1 200", head, StringComparison.Ordinal);
        Assert.Equal(2, dialer.Dialed.Count);
    }

    private static async Task<string> ReadHead(Stream stream)
    {
        var text = new StringBuilder();
        var one = new byte[1];
        while (!text.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal) && await stream.ReadAsync(one, Token) > 0)
        {
            _ = text.Append((char)one[0]);
        }

        return text.ToString();
    }

    /// <summary>A client stream already closed under the proxy: the first write throws ObjectDisposedException, which Reply does not catch (it catches only IOException).</summary>
    private sealed class ClosedStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => throw new ObjectDisposedException("client");

        public override void Write(ReadOnlySpan<byte> buffer) => throw new ObjectDisposedException("client");

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.FromException(new ObjectDisposedException("client"));

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException(new ObjectDisposedException("client"));
    }
}
