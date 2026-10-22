using Aw.Kernel;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core;

/// <summary>Reads the seal file 'validate --seal' names (line spec §9.1): the hash of the hidden set the owner sealed with the plan.</summary>
public interface ISealSource
{
    /// <summary>Reads and parses the seal file at <paramref name="path"/>.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The sealed set's hash; AW-PACKETS-010 when the file is missing, unreadable, malformed or not schemaVersion 1.</returns>
    public ValueTask<Result<PacketHash>> Read(string path, CancellationToken cancellationToken);
}
