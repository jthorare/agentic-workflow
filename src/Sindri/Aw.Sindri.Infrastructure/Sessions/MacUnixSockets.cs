using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>The bound unix sockets on macOS, from lsof: the key lsof's device column gives, so a tmux server's descriptor (see <see cref="MacLsof.OpenFiles"/>) finds its socket path without an inode.</summary>
/// <param name="tools">Runs lsof.</param>
/// <param name="ownUid">The uid whose sockets are listed.</param>
internal sealed class MacUnixSockets(IMacTools tools, int ownUid) : IUnixSockets
{
    public IReadOnlyDictionary<long, string> Read() =>
        tools.Run(MacProcessTable.Lsof, ["-a", "-U", "-u", ownUid.ToString(System.Globalization.CultureInfo.InvariantCulture), "-F", "pftdn"], CancellationToken.None) is { } output
            ? MacLsof.Sockets(output)
            : new Dictionary<long, string>();
}
