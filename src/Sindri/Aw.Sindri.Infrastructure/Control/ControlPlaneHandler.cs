namespace Aw.Sindri.Infrastructure.Control;

/// <summary>
/// The one HTTP handler a CLI process uses for the control plane (#3727): the ping and the request that follows it share one handler, so the ping's connection serves the request and the process starts one socket stack, not two.
/// Loopback only, no proxy; the connect is bounded by <see cref="HttpControlPlaneProbe.Connect"/>, so a dead port still fails fast.
/// </summary>
public static class ControlPlaneHandler
{
    /// <summary>Gets the shared handler. A client that is given it and is disposed disposes it, which ends the process's use of the control plane.</summary>
    public static SocketsHttpHandler Shared { get; } = new() { UseProxy = false, ConnectTimeout = HttpControlPlaneProbe.Connect };
}
