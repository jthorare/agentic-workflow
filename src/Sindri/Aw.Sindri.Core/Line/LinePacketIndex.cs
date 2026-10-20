using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Line;

/// <summary>Reads a line packet's index.json (line spec §3.2): kind, parents and the acceptance chain.</summary>
public static class LinePacketIndex
{
    /// <summary>The parent hashes an index.json lists.</summary>
    /// <param name="indexJson">The text of index.json.</param>
    /// <returns>The parents in order; none when the text is no JSON object or lists no parents.</returns>
    public static IReadOnlyList<string> Parents(string indexJson)
    {
        ArgumentNullException.ThrowIfNull(indexJson);
        try
        {
            return [.. LineJson.Strings(JsonNode.Parse(indexJson), "parents")];
        }
        catch (JsonException)
        {
            return [];   // boundary: the file is text a person or a Step wrote
        }
    }
}
