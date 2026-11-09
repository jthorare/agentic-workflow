using System.Net;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>Where a request to POST /sindri/handoff came from; a test replaces it to play a caller that isn't on this machine.</summary>
internal interface IHandoffPeer
{
    /// <summary>The address of the other end of the connection, or null when it is unknown.</summary>
    /// <param name="context">The request.</param>
    /// <returns>The address.</returns>
    public IPAddress? Of(HttpContext context);
}
