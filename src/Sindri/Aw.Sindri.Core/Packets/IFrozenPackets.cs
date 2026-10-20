using Aw.Kernel;

namespace Aw.Sindri.Core.Packets;

/// <summary>The freeze records aw-packets keeps (packets/&lt;owner&gt;/&lt;name&gt;/&lt;id&gt;/frozen/&lt;hex&gt;.json), looked up by the packet's hash (LN-PKT2's lookup, LN-PKT6).</summary>
public interface IFrozenPackets
{
    /// <summary>Finds the freeze record of a hash, in whatever repository and packet it was frozen under.</summary>
    /// <param name="hash">The hash ('sha256:' and 64 lower-case hex digits).</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The record, or null when the hash was never frozen; an error when the records cannot be read.</returns>
    public ValueTask<Result<FrozenPacket?>> Find(string hash, CancellationToken cancellationToken);
}
