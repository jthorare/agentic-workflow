using Aw.Kernel;

namespace Aw.Sindri.Core.Packets;

/// <summary>
/// aw-packets as a port (deps.yaml Sindri to Packets, kind cli): check, hash and freeze a packet of any kind. A check that fails is an answer, not an error:
/// an error means the tool could not do its job (missing, timed out, a packet it could not read).
/// </summary>
public interface IPackets
{
    /// <summary>Runs the kind's checks.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="cancellationToken">Cancels the run: the tool is killed.</param>
    /// <returns>Whether it passed and what failed; an error when the tool could not run or could not read the packet.</returns>
    public ValueTask<Result<PacketValidation>> Validate(PacketRef packet, CancellationToken cancellationToken);

    /// <summary>The canonical hash, with no check run.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="cancellationToken">Cancels the run: the tool is killed.</param>
    /// <returns>The hash ('sha256:…'); an error when the tool could not run or the packet has no hash.</returns>
    public ValueTask<Result<string>> Hash(PacketRef packet, CancellationToken cancellationToken);

    /// <summary>Runs the checks, then records the hash when they pass. Write-once: a hash frozen before keeps its first record.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="by">Who is freezing it.</param>
    /// <param name="cancellationToken">Cancels the run: the tool is killed.</param>
    /// <returns>What the tool did; an error when it could not run or could not read the packet.</returns>
    public ValueTask<Result<PacketFreeze>> Freeze(PacketRef packet, PacketFreezer by, CancellationToken cancellationToken);
}
