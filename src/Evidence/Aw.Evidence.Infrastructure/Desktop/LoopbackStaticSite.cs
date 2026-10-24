using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Desktop;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Desktop;

/// <summary>A folder served read-only on 127.0.0.1 and a free port: GET and HEAD, files below the folder only.</summary>
internal sealed class LoopbackStaticSite : IStaticSite
{
    private const int Attempts = 5;

    /// <inheritdoc/>
    public ValueTask<Result<IServedSite>> ServeAsync(string directory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(directory))
        {
            return ValueTask.FromResult<Result<IServedSite>>(EvidenceErrors.RunFailed.With($"the design folder {directory} does not exist"));
        }

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var port = FreePort();
            var listener = new HttpListener();
            listener.Prefixes.Add(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/"));
            try
            {
                listener.Start();
            }
            catch (HttpListenerException)
            {
                listener.Close();   // the port was taken between looking and binding; try another
                continue;
            }

            return ValueTask.FromResult<Result<IServedSite>>(new ServedSite(listener, string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}"), Path.GetFullPath(directory)));
        }

        return ValueTask.FromResult<Result<IServedSite>>(EvidenceErrors.RunFailed.With("no free loopback port for the design pages"));
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}
