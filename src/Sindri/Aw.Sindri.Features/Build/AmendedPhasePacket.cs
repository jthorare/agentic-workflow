using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Build;

/// <summary>
/// The child phase packet of a scope grant (line spec §3.2 row 4, §7.2, LN-PKT1): the parent's files, with the id's next amendment number, 'draft' status, the granted paths added
/// to 'allowedFiles', 'amends: { parent, paths, reason, at }' in phase.json and the parent's hash as the only parent in index.json. Every other file is the parent's, byte for byte,
/// so the acceptance links still hold (P-006).
/// </summary>
/// <param name="Id">The child's packet id ('&lt;line&gt;-&lt;phase&gt;-a&lt;n&gt;').</param>
/// <param name="Files">File name to text.</param>
internal sealed record AmendedPhasePacket(string Id, IReadOnlyDictionary<string, string> Files)
{
    internal static Result<AmendedPhasePacket> From(IReadOnlyDictionary<string, string> parent, string parentHash, ScopeGrant grant, DateTimeOffset at)
    {
        var packet = Object(parent, "packet.json");
        var phase = Object(parent, "phase.json");
        var index = Object(parent, "index.json");
        if (packet is null || phase is null || index is null || StepFacts.Field(packet, "id") is not { } id)
        {
            return SindriErrors.InvalidInput.With("the phase packet being amended must hold packet.json (with an id), phase.json and index.json, each a JSON object");
        }

        var next = NextId(id);
        packet["id"] = next;
        packet["status"] = "draft";
        packet["hash"] = null;
        phase["allowedFiles"] = Granted(phase["allowedFiles"], grant.Paths);
        phase["amends"] = new JsonObject { ["parent"] = parentHash, ["paths"] = new JsonArray([.. grant.Paths.Select(static p => (JsonNode?)JsonValue.Create(p))]), ["reason"] = grant.Reason, ["at"] = at.ToString("O", CultureInfo.InvariantCulture) };
        index["parents"] = new JsonArray(JsonValue.Create(parentHash));
        var files = new Dictionary<string, string>(parent, StringComparer.Ordinal)
        {
            ["packet.json"] = packet.ToJsonString(),
            ["phase.json"] = phase.ToJsonString(),
            ["index.json"] = index.ToJsonString(),
        };
        return new AmendedPhasePacket(next, files);
    }

    /// <summary>The id of the amendment of <paramref name="id"/>: '-a1' after a first packet, the next number after an amendment.</summary>
    internal static string NextId(string id)
    {
        var cut = id.LastIndexOf("-a", StringComparison.Ordinal);
        return cut > 0 && int.TryParse(id[(cut + 2)..], NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? string.Create(CultureInfo.InvariantCulture, $"{id[..cut]}-a{n + 1}")
            : id + "-a1";
    }

    private static JsonArray Granted(JsonNode? allowed, IReadOnlyList<string> paths)
    {
        var all = (allowed as JsonArray)?.Select(static n => n?.GetValue<string>()).OfType<string>().ToList() ?? [];
        all.AddRange(paths.Where(p => !all.Contains(p, StringComparer.Ordinal)));
        return new JsonArray([.. all.Select(static p => (JsonNode?)JsonValue.Create(p))]);
    }

    private static JsonObject? Object(IReadOnlyDictionary<string, string> files, string name)
    {
        try
        {
            return files.TryGetValue(name, out var text) ? JsonNode.Parse(text) as JsonObject : null;
        }
        catch (JsonException)
        {
            return null;   // boundary: a file that is not JSON is the same answer as a missing one
        }
    }
}
