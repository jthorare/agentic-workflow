using System.Net;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The peer of the request's own connection.</summary>
internal sealed class ConnectionHandoffPeer : IHandoffPeer
{
    public IPAddress? Of(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Connection.RemoteIpAddress;
    }
}
