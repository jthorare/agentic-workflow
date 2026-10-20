using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Packets;

namespace Aw.Sindri.Host.Commands.Scoping;

/// <summary>aw-packets, run as a program (deps.yaml Sindri to Packets, kind cli): the one canonical hash of a packet directory (scoping spec §8.3, P1), through the same port the services use.</summary>
internal static class PacketsCli
{
    /// <summary>The packet directory's canonical hash ('sha256:…') from 'aw-packets hash --kind scoping &lt;dir&gt;'. The program is AW_PACKETS_BIN, else 'aw-packets' on the PATH.</summary>
    internal static async Task<Result<string>> Hash(IPackets packets, string packetDirectory, CancellationToken ct)
    {
        var hash = await packets.Hash(new PacketRef(PacketKind.Scoping, packetDirectory), ct).ConfigureAwait(false);
        return hash.IsError ? SindriErrors.InvalidInput.With(hash.Error.Message) : hash;
    }
}
