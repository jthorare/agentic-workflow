using Aw.Evidence.Core.Data;
using Aw.Evidence.Infrastructure.Http;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The real transport against a local HTTP server on loopback.</summary>
[Lane(TestLane.Integration)]
public sealed class HttpApiTransportContractTests : ApiTransportContract, IDisposable
{
    private readonly List<IDisposable> _owned = [];

    protected override Task<ApiUnderTest> ArrangeAsync(IReadOnlyDictionary<string, HttpReply> routes)
    {
        var pages = new Dictionary<string, string>(StringComparer.Ordinal);
        var redirects = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, reply) in routes)
        {
            var path = key[(key.IndexOf(' ', StringComparison.Ordinal) + 1)..];
            if (reply.Status == 302)
            {
                redirects[path] = reply.Location ?? "/";
            }
            else
            {
                pages[path] = reply.Body;
            }
        }

        var server = new LocalHttpServer(pages, redirects);
        var transport = new HttpApiTransport(TimeSpan.FromSeconds(10));
        _owned.Add(server);
        _owned.Add(transport);
        return Task.FromResult(new ApiUnderTest(transport, server.Origin, () => [.. server.Received, .. server.Probes]));
    }

    protected override Task<ApiUnderTest> ArrangeUnreachableAsync()
    {
        var transport = new HttpApiTransport(TimeSpan.FromSeconds(10));
        _owned.Add(transport);
        _owned.Add(LocalHttpServer.ClosedPort(out var port));
        return Task.FromResult(new ApiUnderTest(transport, "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture), () => []));
    }

    public void Dispose()
    {
        foreach (var owned in _owned)
        {
            owned.Dispose();
        }
    }
}
