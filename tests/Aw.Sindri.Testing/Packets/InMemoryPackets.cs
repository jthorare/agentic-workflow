using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Packets;

namespace Aw.Sindri.Testing.Packets;

/// <summary>
/// The in-memory <see cref="IPackets"/>: a packet is good unless <see cref="Fails"/> named its directory. The hash comes from the kind and the directory, so it is stable;
/// a freeze is write-once like aw-packets': the first freezer stays. A directory registered with <see cref="Unreadable"/> is an error, as a packet aw-packets could not read is.
/// </summary>
public sealed class InMemoryPackets : IPackets
{
    private readonly Dictionary<string, IReadOnlyList<PacketFailure>> _failing = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unreadable = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PacketFreezer> _frozen = new(StringComparer.Ordinal);
    private readonly List<(string Verb, PacketRef Packet, PacketFreezer? By)> _calls = [];

    /// <summary>Every call so far, in order.</summary>
    public IReadOnlyList<(string Verb, PacketRef Packet, PacketFreezer? By)> Calls => _calls;

    /// <summary>Makes the packet in a directory fail its checks.</summary>
    /// <param name="directory">The packet directory.</param>
    /// <param name="failures">What failed; one generic failure when none are given.</param>
    /// <returns>The same instance.</returns>
    public InMemoryPackets Fails(string directory, params PacketFailure[] failures)
    {
        _failing[directory] = failures.Length > 0 ? failures : [new PacketFailure("PKT-TEST-001", "packet", "the packet fails", "fix it")];
        return this;
    }

    /// <summary>Makes the packet in a directory unreadable: every call on it is an error.</summary>
    /// <param name="directory">The packet directory.</param>
    /// <returns>The same instance.</returns>
    public InMemoryPackets Unreadable(string directory)
    {
        _ = _unreadable.Add(directory);
        return this;
    }

    /// <summary>The hash the fake gives the packet.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>'sha256:' and 64 hex digits.</returns>
    public static string HashOf(PacketRef packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(packet.Kind + "|" + packet.Directory)));
    }

    /// <inheritdoc/>
    public ValueTask<Result<PacketValidation>> Validate(PacketRef packet, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add(("validate", packet, null));
        return ValueTask.FromResult(Check(packet).Map(static failures => new PacketValidation(failures.Count == 0, failures)));
    }

    /// <inheritdoc/>
    public ValueTask<Result<string>> Hash(PacketRef packet, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add(("hash", packet, null));
        return ValueTask.FromResult(_unreadable.Contains(packet.Directory) ? NotReadable(packet.Directory, "hash") : Results.Success(HashOf(packet)));
    }

    /// <inheritdoc/>
    public ValueTask<Result<PacketFreeze>> Freeze(PacketRef packet, PacketFreezer by, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add(("freeze", packet, by));
        return ValueTask.FromResult(Check(packet).Map(failures =>
        {
            if (failures.Count > 0)
            {
                return new PacketFreeze(Frozen: false, Hash: null, By: by, AlreadyFrozen: false, AlreadyFrozenBy: null, Failures: failures);
            }

            var hash = HashOf(packet);
            if (_frozen.TryGetValue(hash, out var first))
            {
                return new PacketFreeze(Frozen: true, Hash: hash, By: first, AlreadyFrozen: true, AlreadyFrozenBy: first == by ? null : first, Failures: []);
            }

            _frozen[hash] = by;
            return new PacketFreeze(Frozen: true, Hash: hash, By: by, AlreadyFrozen: false, AlreadyFrozenBy: null, Failures: []);
        }));
    }

    private Result<IReadOnlyList<PacketFailure>> Check(PacketRef packet)
    {
        if (_unreadable.Contains(packet.Directory))
        {
            return NotReadable(packet.Directory, "check");
        }

        return Results.Success(_failing.TryGetValue(packet.Directory, out var failures) ? failures : []);
    }

    private static Error NotReadable(string directory, string verb) => SindriErrors.InvalidInput.With($"aw-packets {verb} failed: {directory} is not a readable packet");
}
