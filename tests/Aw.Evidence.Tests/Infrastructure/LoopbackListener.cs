using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>Starts a loopback <see cref="HttpListener"/>, taking a fresh port when the probed one was taken in between (#2389; same shape as Aw.Sindri.Testing's LoopbackListener).</summary>
internal static class LoopbackListener
{
    internal const int StartAttempts = 5;

    internal static (HttpListener Listener, int Port) Start()
    {
        for (var attempt = 1; ; attempt++)
        {
            var port = FreePort();
            var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/");
            try
            {
                listener.Start();
                return (listener, port);
            }
            catch (HttpListenerException exception) when (attempt < StartAttempts && IsAddressInUse(exception))
            {
                listener.Close();
            }
            catch
            {
                listener.Close();
                throw;
            }
        }
    }

    internal static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private static bool IsAddressInUse(HttpListenerException exception) =>
        exception.ErrorCode is 98 or 48 or 10048 or 32 or 183
        || exception.Message.Contains("in use", StringComparison.OrdinalIgnoreCase);
}
