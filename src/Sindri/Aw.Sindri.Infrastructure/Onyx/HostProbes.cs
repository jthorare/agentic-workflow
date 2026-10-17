using System.Net;
using System.Net.Sockets;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>IHostProbes over the file system and loopback sockets: DriveInfo of the nearest existing ancestor, and a connect and a brief bind per loopback port.</summary>
internal sealed class HostProbes(bool ipv6Available) : IHostProbes
{
    public HostProbes() : this(Socket.OSSupportsIPv6)
    {
    }

    /// <inheritdoc />
    public DiskFacts FreeDisk(string path)
    {
        var measured = Path.GetFullPath(path);
        while (!Directory.Exists(measured) && Path.GetDirectoryName(measured) is { Length: > 0 } parent)
        {
            measured = parent;
        }

        return new DiskFacts(measured, new DriveInfo(measured).AvailableFreeSpace);
    }

    /// <inheritdoc />
    public DiskFacts? FreeDiskIfExists(string path)
    {
        var full = Path.GetFullPath(path);
        return Directory.Exists(full) ? new DiskFacts(full, new DriveInfo(full).AvailableFreeSpace) : null;
    }

    /// <inheritdoc />
    public PortFacts Ports(int preferred, int fallbackFirst, int fallbackLast) => Ports(preferred, fallbackFirst, fallbackLast, IsFree);

    // The probe is a parameter so a test can record the order: the preferred port first, the fallbacks only when it is taken.
    internal static PortFacts Ports(int preferred, int fallbackFirst, int fallbackLast, Func<int, bool> isFree)
    {
        if (isFree(preferred))
        {
            return new PortFacts(preferred, PreferredFree: true, FirstFreeFallback: null);
        }

        int? fallback = null;
        for (var port = fallbackFirst; port <= fallbackLast && fallback is null; port++)
        {
            fallback = isFree(port) ? port : null;
        }

        return new PortFacts(preferred, PreferredFree: false, fallback);
    }

    // Free means nothing answers on 127.0.0.1:<port> or [::1]:<port> and 127.0.0.1:<port> can be bound. The connects matter:
    // on BSD and macOS a bind to 127.0.0.1 can succeed beside another process listening on 0.0.0.0 (what Docker publishes) or
    // on ::1 only, so a bind alone would call that port free. A probe that can't tell counts as taken: the conservative answer.
    private bool IsFree(int port)
    {
        try
        {
            return !Answers(IPAddress.Loopback, port) && !(ipv6Available && Answers(IPAddress.IPv6Loopback, port)) && CanBind(port);
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException or InvalidOperationException or AggregateException)
        {
            return false;
        }
    }

    private static bool Answers(IPAddress address, int port)
    {
        try
        {
            using var client = new TcpClient(address.AddressFamily);
            return client.ConnectAsync(address, port).Wait(TimeSpan.FromMilliseconds(500)) && client.Connected;
        }
        catch (SocketException exception) when (exception.SocketErrorCode is SocketError.AddressFamilyNotSupported)
        {
            return false;   // this box has no such loopback: nothing listens there
        }
        catch (AggregateException exception) when (exception.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused or SocketError.AddressNotAvailable or SocketError.AddressFamilyNotSupported or SocketError.NetworkUnreachable or SocketError.HostUnreachable })
        {
            return false;   // refused, or this box has no such loopback: nothing listens there
        }
    }

    // Exclusive use only on Windows. On Unix `false` sets SO_REUSEADDR, so a port left in TIME_WAIT by `onyx down` (the stack's own connections closed first) reads free instead of
    // making the next `onyx up` move to another port; the two connect probes, which don't depend on it, stay the authority that nothing listens there.
    private static bool CanBind(int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port) { ExclusiveAddressUse = OperatingSystem.IsWindows() };
        try
        {
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            listener.Stop();
        }
    }
}
