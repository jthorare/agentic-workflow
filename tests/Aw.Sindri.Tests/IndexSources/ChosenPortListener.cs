using System.Net;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.IndexSources;

/// <summary>Starts a loopback listener on a freshly probed port, retrying a taken port through <see cref="LoopbackListener"/>.</summary>
internal static class ChosenPortListener
{
    // why: LinearTokenOrderTests starts its listener in its constructor, so a test can't reach the start there. This is the start, with a seam (#3415) called with every chosen port after the probe frees it and before the bind.
    internal static (HttpListener Listener, int Port) Start(Action<int>? afterPortChosen = null)
    {
        var (listener, baseUri) = LoopbackListener.Start(() =>
        {
            var port = LoopbackListener.FreePort();
            afterPortChosen?.Invoke(port);
            return port;
        });
        return (listener, baseUri.Port);
    }
}
