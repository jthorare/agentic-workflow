using System.Globalization;
using System.Net;
using Aw.Sindri.Core.Profile;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// The port 'sindri serve' listens on, chosen from the app's own container before it binds: AW_SINDRI_PORT when set,
/// else the approved profile's api.port, else 7410. Kestrel reads its options when the app is built, so serve's host adds
/// no listener then (<see cref="LoopbackBinding"/>) and this adds the one loopback listener once the port is known.
/// </summary>
internal sealed class ServePort(IApprovals approvals, IOptions<KestrelServerOptions> kestrel)
{
    /// <summary>
    /// The chosen port, or null until <see cref="Listen"/> has run. While it is null serve's host refuses to start
    /// (<see cref="ServeListenerCheck"/>), and ServeCommand refuses before it takes the lease.
    /// </summary>
    public int? Chosen { get; private set; }

    /// <summary>Chooses the port and adds the loopback listener on it.</summary>
    /// <param name="fromEnvironment">AW_SINDRI_PORT's text (already checked to be a port), or null or empty when unset.</param>
    /// <param name="cancellationToken">Cancels the read of the approval.</param>
    /// <returns>The port.</returns>
    public async ValueTask<int> Listen(string? fromEnvironment, CancellationToken cancellationToken)
    {
        if (!int.TryParse(fromEnvironment, NumberStyles.None, CultureInfo.InvariantCulture, out var port))
        {
            var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
            port = (approved.IsSuccess ? approved.Value?.Profile.Api?.Port : null) ?? ApiSettings.DefaultPort;
        }

        Chosen = port;
        kestrel.Value.Listen(IPAddress.Loopback, port);
        return port;
    }
}
