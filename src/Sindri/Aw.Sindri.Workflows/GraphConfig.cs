using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Interpolation;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Replaces every '{{ref}}' in a node's config with its value.</summary>
internal static class GraphConfig
{
    /// <summary>Interpolates a config.</summary>
    /// <param name="config">The config as written.</param>
    /// <param name="lookup">Reads a reference; null for a missing value.</param>
    /// <returns>A new config; <paramref name="config"/> is not changed.</returns>
    public static JsonObject Interpolate(JsonObject config, Func<string, JsonNode?> lookup)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new JsonObject(config.Select(p => KeyValuePair.Create(p.Key, Walk(p.Value, lookup))));
    }

    private static JsonNode? Walk(JsonNode? node, Func<string, JsonNode?> lookup) => node switch
    {
        JsonObject o => new JsonObject(o.Select(p => KeyValuePair.Create(p.Key, Walk(p.Value, lookup)))),
        JsonArray a => new JsonArray([.. a.Select(i => Walk(i, lookup))]),
        JsonValue v when v.TryGetValue<string>(out var text) => Template.Render(text, lookup),
        null => null,
        _ => node.DeepClone(),
    };
}
