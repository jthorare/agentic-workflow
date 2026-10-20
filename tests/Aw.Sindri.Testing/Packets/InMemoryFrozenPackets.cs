using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Packets;

namespace Aw.Sindri.Testing.Packets;

/// <summary>The in-memory <see cref="IFrozenPackets"/>: the records it was given, by hash.</summary>
public sealed class InMemoryFrozenPackets : IFrozenPackets
{
    private readonly Dictionary<string, FrozenPacket> _records;

    /// <summary>Holds the records.</summary>
    /// <param name="held">The freeze records.</param>
    public InMemoryFrozenPackets(IEnumerable<FrozenPacket> held)
    {
        ArgumentNullException.ThrowIfNull(held);
        _records = held.ToDictionary(static r => r.Hash, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public ValueTask<Result<FrozenPacket?>> Find(string hash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsHash(hash))
        {
            return ValueTask.FromResult<Result<FrozenPacket?>>(SindriErrors.InvalidInput.With($"'{hash}' is not a packet hash ('sha256:' and 64 lower-case hex digits)"));
        }

        return ValueTask.FromResult(Results.Success(_records.GetValueOrDefault(hash)));
    }

    private static bool IsHash(string hash) => hash.Length == 71 && hash.StartsWith("sha256:", StringComparison.Ordinal) && hash[7..].All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
