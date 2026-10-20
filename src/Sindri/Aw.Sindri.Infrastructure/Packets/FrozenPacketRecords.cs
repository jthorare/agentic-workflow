using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Infrastructure.Packets;

/// <summary>
/// <see cref="IFrozenPackets"/> over aw-packets' own freeze records on disk: packets/&lt;owner&gt;/&lt;name&gt;/&lt;id&gt;/frozen/&lt;hex&gt;.json under $AW_STATE_DIR (else ~/.agentic-workflow).
/// Read only; the hash is checked for shape before it becomes part of a path, and a record whose own hash is not the one asked for is not a freeze of it.
/// </summary>
/// <param name="packetsRoot">The packets/ directory.</param>
internal sealed class FrozenPacketRecords(string packetsRoot) : IFrozenPackets
{
    public ValueTask<Result<FrozenPacket?>> Find(string hash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsHash(hash))
        {
            return ValueTask.FromResult<Result<FrozenPacket?>>(SindriErrors.InvalidInput.With($"'{hash}' is not a packet hash ('sha256:' and 64 lower-case hex digits)"));
        }

        try
        {
            return ValueTask.FromResult(Results.Success(Read(hash)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult<Result<FrozenPacket?>>(ScopingErrors.PacketToolFailed.With($"the freeze records under {packetsRoot} can't be read: {e.Message}"));
        }
    }

    private FrozenPacket? Read(string hash)
    {
        if (!Directory.Exists(packetsRoot))
        {
            return null;
        }

        var name = hash[7..] + ".json";
        foreach (var file in Directory.EnumerateFiles(packetsRoot, name, SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(packetsRoot, file).Split(Path.DirectorySeparatorChar);
            if (relative is [_, _, _, "frozen", _] && Record(file, hash) is { } record)
            {
                return record;
            }
        }

        return null;
    }

    private static FrozenPacket? Record(string file, string hash)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject record || Text(record, "hash") != hash || Text(record, "packet") is not { Length: > 0 } packet)
            {
                return null;
            }

            return new FrozenPacket(hash, packet, Text(record, "kind") ?? "build", string.Equals(Text(record, "by"), "owner", StringComparison.Ordinal) ? PacketFreezer.Owner : PacketFreezer.Machine);
        }
        catch (JsonException)
        {
            return null;   // boundary: a file under packets/ that is no freeze record is not one
        }
    }

    private static string? Text(JsonObject record, string name) => record[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static bool IsHash(string hash) => hash.Length == 71 && hash.StartsWith("sha256:", StringComparison.Ordinal) && hash[7..].All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
