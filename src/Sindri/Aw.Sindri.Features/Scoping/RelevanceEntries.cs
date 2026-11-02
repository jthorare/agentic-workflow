using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>Reads the entries of the lenses fact.</summary>
internal static class RelevanceEntries
{
    /// <summary>Every entry of the lenses fact, lens by lens in the fixed lens order, a lens's nodes before its items. An element without a lens item id is not an entry.</summary>
    /// <param name="lenses">The lenses fact.</param>
    /// <returns>The entries.</returns>
    internal static IReadOnlyList<RelevanceEntry> Of(JsonNode? lenses)
    {
        var entries = new List<RelevanceEntry>();
        foreach (var lens in ScopeRules.Lenses)
        {
            var found = ScopeJson.Obj(lenses, lens);
            entries.AddRange(ScopeJson.Items(found, "nodes").Select(node => Node(lens, node)).OfType<RelevanceEntry>());
            entries.AddRange(ScopeJson.Items(found, "items").Select(item => Item(lens, item)).OfType<RelevanceEntry>());
        }

        return entries;
    }

    private static RelevanceEntry? Node(string lens, JsonNode node) =>
        ScopeJson.Str(node, "lensItem") is { } id
            ? new RelevanceEntry(id, lens, ScopeJson.Str(node, "key"), ScopeJson.Str(node, "kind") ?? "node", ScopeJson.Str(node, "label") ?? ScopeJson.Str(node, "key") ?? id, ScopeJson.Str(node, "band"))
            : null;

    private static RelevanceEntry? Item(string lens, JsonNode item) =>
        ScopeJson.Str(item, "lensItem") is { } id
            ? new RelevanceEntry(id, lens, Key: null, ScopeJson.Str(item, "kind") ?? "item", ScopeJson.Str(item, "label") ?? id, Band: null)
            : null;
}
