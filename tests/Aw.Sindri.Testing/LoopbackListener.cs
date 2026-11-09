using System.Net;

namespace Aw.Sindri.Testing;

/// <summary>Starts the loopback <see cref="HttpListener"/> every Testing fake serves from.</summary>
public static class LoopbackListener
{
    /// <summary>How many ports <see cref="Start"/> tries before it gives up.</summary>
    public const int StartAttempts = 5;

    /// <summary>
    /// Starts an <see cref="HttpListener"/> on http://127.0.0.1:&lt;port&gt;/. <paramref name="nextPort"/> defaults to a TcpListener-on-0 probe.
    /// When the bind fails with address-in-use it tries a fresh port with a new listener, up to <see cref="StartAttempts"/>, then throws the last <see cref="HttpListenerException"/>.
    /// </summary>
    /// <param name="nextPort">Gives the next port to try; null probes a free one.</param>
    /// <returns>The started listener and its base URI.</returns>
    public static (HttpListener Listener, Uri BaseUri) Start(Func<int>? nextPort = null) => throw new NotSupportedException();
}
