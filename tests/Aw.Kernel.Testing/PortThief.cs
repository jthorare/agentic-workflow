using System.Net;
using System.Net.Sockets;

namespace Aw.Kernel.Testing;

/// <summary>
/// Plays "another process takes this port" in a test that forces a port race (#3451). A real other process on a busy box
/// can take the port first; then the port is taken either way, so that counts as taken and the test still checks its case.
/// </summary>
public static class PortThief
{
    /// <summary>Binds <paramref name="port"/> on loopback and holds it until the caller stops the listener.</summary>
    /// <param name="port">The port to take.</param>
    /// <returns>The listener that holds the port, or null when something else already holds it.</returns>
    public static TcpListener? TryTake(int port)
    {
        var thief = new TcpListener(IPAddress.Loopback, port);
        try
        {
            thief.Start();
            return thief;
        }
        catch (SocketException exception) when (exception.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            thief.Dispose();
            return null;
        }
    }
}
