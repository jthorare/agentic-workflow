using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// The HTTP adapter over a real loopback server and a real temp directory.
[Lane(TestLane.Integration)]
public sealed class HttpDeployFilesContractTests : DeployFilesContract
{
    protected override DeployFilesRig Create(IReadOnlyDictionary<string, byte[]> remote, IReadOnlyDictionary<string, byte[]> local)
        => HttpRig.Start(remote, local);

    internal sealed class HttpRig : DeployFilesRig
    {
        private readonly LoopbackFileServer _server;
        private readonly HttpClient _client;
        private readonly string _dir;

        private HttpRig(LoopbackFileServer server, HttpClient client, string dir)
        {
            (_server, _client, _dir) = (server, client, dir);
            Files = new HttpDeployFiles(client, server.BaseUri);
        }

        public override IDeployFiles Files { get; }

        public override string DeployDir => _dir;

        public override IReadOnlyList<string> Requested => _server.Requests;

        public static HttpRig Start(IReadOnlyDictionary<string, byte[]> remote, IReadOnlyDictionary<string, byte[]> local)
        {
            var served = remote.ToDictionary(static f => "/" + new string('a', 40) + "/deployment/" + f.Key, static f => f.Value, StringComparer.Ordinal);
            var dir = Path.Combine(Path.GetTempPath(), "aw-deploy-" + Guid.NewGuid().ToString("N"));
            foreach (var (path, bytes) in local)
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dir, path))!);   // why: the path always has a directory
                File.WriteAllBytes(Path.Combine(dir, path), bytes);
            }

            return new HttpRig(new LoopbackFileServer(served), new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }), dir);
        }

        public override byte[]? ReadLocal(string path)
        {
            var full = Path.Combine(_dir, path);
            return File.Exists(full) ? File.ReadAllBytes(full) : null;
        }

        public override void Dispose()
        {
            _server.Dispose();
            _client.Dispose();
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }

            base.Dispose();
        }
    }
}
