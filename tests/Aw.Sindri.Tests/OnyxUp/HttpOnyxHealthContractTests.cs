using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// The HTTP adapter over a real loopback server. The server listens on the port the adapter probes (the adapter only talks to http://127.0.0.1:<port>).
[Lane(TestLane.Integration)]
public sealed class HttpOnyxHealthContractTests : OnyxHealthContract
{
    protected override OnyxHealthRig Create(OnyxServerScript script) => new HttpRig(script);

    private sealed class HttpRig : OnyxHealthRig
    {
        private readonly LoopbackFileServer? _server;
        private readonly HttpClient _client = new();

        public HttpRig(OnyxServerScript script)
        {
            if (script.Listening)
            {
                _server = new LoopbackFileServer(new Dictionary<string, byte[]>(StringComparer.Ordinal) { [OnyxRoutes.Version] = System.Text.Encoding.UTF8.GetBytes(script.VersionBody), [OnyxRoutes.Health] = "ok"u8.ToArray() });
                _server.Statuses[OnyxRoutes.Health] = script.HealthStatus;
                _server.Statuses[OnyxRoutes.Version] = script.VersionStatus;
                if (script.VersionStatus == 200)
                {
                    _ = _server.Statuses.Remove(OnyxRoutes.Version);
                }

                if (script.HealthStatus == 200)
                {
                    _ = _server.Statuses.Remove(OnyxRoutes.Health);
                }
            }

            Port = _server?.BaseUri.Port ?? LoopbackFileServer.FreePort();
            Health = new HttpOnyxHealth(_client);
        }

        public override IOnyxHealth Health { get; }

        public override int Port { get; }

        public override void Dispose()
        {
            _server?.Dispose();
            _client.Dispose();
            base.Dispose();
        }
    }
}
