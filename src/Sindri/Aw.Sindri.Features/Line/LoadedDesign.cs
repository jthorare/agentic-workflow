using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Packets;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// Loads a candidate design packet into a 'fact:design' (<see cref="DesignPacket.FromFiles"/>): verifies its hash against its directory (SND-LINE-001) and that its index names the
/// scope packet as a parent. The plan station's intake and a design revision's intake share it.
/// </summary>
internal static class LoadedDesign
{
    internal static async ValueTask<Result<JsonObject>> Load(IPackets packets, IPacketFiles files, JsonNode? fact, (string? ScopeHash, string FactName) origin, CancellationToken cancellationToken)
    {
        var packet = PacketFact.Of(fact, origin.FactName);
        if (packet.IsError)
        {
            return packet.Error;
        }

        var verified = await LinePackets.Verify(packets, PacketKind.Design, packet.Value, cancellationToken).ConfigureAwait(false);
        if (verified.IsError)
        {
            return verified.Error;
        }

        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in DesignPacket.FileNames.Append("index.json"))
        {
            var read = await files.Read(packet.Value.Directory, name, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            texts[name] = read.Value;
        }

        return NamesParent(texts["index.json"], origin.ScopeHash)
            ? DesignPacket.FromFiles(texts, packet.Value)
            : SindriErrors.InvalidInput.With($"SND-LINE-001: the design packet's index.json does not name the scope packet {origin.ScopeHash} as a parent (fix: re-run the design station on the frozen scope packet)");
    }

    private static bool NamesParent(string index, string? scopeHash)
    {
        try
        {
            return scopeHash is not null && LineJson.Strings(JsonNode.Parse(index), "parents").Contains(scopeHash, StringComparer.Ordinal);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;   // boundary: a packet file is text a model wrote
        }
    }
}
