using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core;

/// <summary>Parses a holdout seal file, <c language="text">{schemaVersion, phase, setHash, planHash}</c> as Sindri writes it: strict JSON, schemaVersion 1, a 'sha256:' setHash. Pure: every ISealSource adapter supplies the bytes.</summary>
public static class SealReader
{
    /// <summary>Parses <paramref name="bytes"/>.</summary>
    /// <param name="name">The file's name, for messages.</param>
    /// <param name="bytes">The file.</param>
    /// <returns>The sealed set's hash, or AW-PACKETS-010 saying what is wrong.</returns>
    public static Result<PacketHash> Parse(string name, ReadOnlyMemory<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (PacketFileShape.StrictProblem(name, bytes.Span) is { } problem)
        {
            return PacketsErrors.SealUnusable.With(problem);
        }

        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return PacketsErrors.SealUnusable.With($"{name}: a seal is a JSON object.");
            }

            if (!root.TryGetProperty("schemaVersion", out var version) || !version.TryGetInt32(out var v) || v != PacketReader.SchemaVersion)
            {
                return PacketsErrors.SealUnusable.With(string.Create(CultureInfo.InvariantCulture, $"{name}: schemaVersion must be {PacketReader.SchemaVersion}; this build reads {PacketReader.SchemaVersion}."));
            }

            return root.TryGetProperty("setHash", out var set) && set.ValueKind == JsonValueKind.String && PacketHash.TryFrom(set.GetString() ?? string.Empty, out var hash)
                ? hash
                : PacketsErrors.SealUnusable.With($"{name}: setHash must be 'sha256:' and 64 lower-case hex digits.");
        }
        catch (JsonException e)
        {
            return PacketsErrors.SealUnusable.With($"{name}: {e.Message}");   // boundary: untrusted file
        }
    }
}
