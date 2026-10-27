using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// The evidence step's code (scoping spec §8.3): moves an uncited item out of the map into 'flagged', stamps trust, and assembles the
/// fourteen packet files from the facts. Pure: the files come back as text for the caller to write.
/// </summary>
internal static class ScopeEvidence
{
    /// <summary>The packet directory the Step writes under the run area.</summary>
    internal const string PacketDirectory = "scope-packet";

    /// <summary>The facts with every surface, implication and acceptance item that cites nothing that resolves moved to 'flagged' (SCP-CITE-002); an acceptance item never reaches the list.</summary>
    internal static ScopeFacts FlagUncited(ScopeFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts["scopeMap"] is not JsonObject map)
        {
            return facts;
        }

        var ids = new ScopeIds(facts);
        var clone = (JsonObject)map.DeepClone();
        var flagged = ScopeJson.Arr(clone, "flagged") ?? [];
        clone["flagged"] = flagged;
        var gone = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in new[] { "surfaces", "implications" })
        {
            Strip(clone, name, ids, flagged, gone);
        }

        var acceptance = facts["acceptance"] is JsonObject source ? (JsonObject)source.DeepClone() : null;
        if (acceptance is not null)
        {
            Strip(acceptance, "items", ids, flagged, gone);
            foreach (var item in ScopeJson.Items(acceptance, "items").OfType<JsonObject>())
            {
                item["surfaces"] = ScopeJson.ToArray(ScopeJson.Strings(item, "surfaces").Where(s => !gone.Contains(s)));
            }
        }

        foreach (var workstream in ScopeJson.Items(clone, "workstreams").OfType<JsonObject>())
        {
            workstream["surfaces"] = ScopeJson.ToArray(ScopeJson.Strings(workstream, "surfaces").Where(s => !gone.Contains(s)));
        }

        var result = facts.With("scopeMap", clone);
        return acceptance is null ? result : result.With("acceptance", acceptance);
    }

    private static void Strip(JsonObject owner, string list, ScopeIds ids, JsonArray flagged, HashSet<string> gone)
    {
        if (owner[list] is not JsonArray array)
        {
            return;
        }

        var kept = new JsonArray();
        foreach (var item in array.OfType<JsonNode>())
        {
            var citations = ScopeJson.Strings(item, "citations").ToList();
            if (citations.Exists(ids.Resolves))
            {
                ScopeJson.Append(kept, item.DeepClone());
                continue;
            }

            var id = ScopeJson.Str(item, "id") ?? "?";
            _ = gone.Add(id);
            ScopeJson.Append(flagged, new JsonObject
            {
                ["item"] = id,
                ["reason"] = citations.Count == 0 ? "cites nothing" : $"none of {string.Join(", ", citations)} resolves",
            });
        }

        owner[list] = kept;
    }
}
