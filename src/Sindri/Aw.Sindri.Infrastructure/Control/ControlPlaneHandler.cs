namespace Aw.Sindri.Infrastructure.Control;

/// <summary>
/// The HTTP handler a CLI process uses for the control plane (#3727): the ping and the request that follows it share one, so the ping's connection serves the request and the process starts one socket stack, not two.
/// Loopback only, no proxy; the connect is bounded by <see cref="HttpControlPlaneProbe.Connect"/>, so a dead port still fails fast. The client container registers it as the one <see cref="HttpMessageHandler"/>.
/// </summary>
internal static class ControlPlaneHandler
{
    /// <summary>Makes the handler.</summary>
    /// <returns>A new handler the caller owns.</returns>
    internal static SocketsHttpHandler Create() => new() { UseProxy = false, ConnectTimeout = HttpControlPlaneProbe.Connect };
}
