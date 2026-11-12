using System.Net;
using System.Net.Sockets;

namespace Aw.Sindri.Tests.IndexSources;

/// <summary>Starts a loopback listener on a freshly probed port.</summary>
internal static class ChosenPortListener
{
    // why: LinearTokenOrderTests starts its listener in its constructor, so a test can't reach the start there. This is the start, with a seam (#3415) called with the chosen port after the probe frees it and before the bind.
    internal static (HttpListener Listener, int Port) Start(Action<int>? afterPortChosen = null)
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        afterPortChosen?.Invoke(port);
        var listener = new HttpListener();
        listener.Prefixes.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/"));
        listener.Start();
        return (listener, port);
    }
}
