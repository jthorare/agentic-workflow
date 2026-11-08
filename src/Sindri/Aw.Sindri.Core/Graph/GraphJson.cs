using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph;

/// <summary>Reads a graph spec from its JSON text, writes it back and hashes it.</summary>
public static class GraphJson
{
    private static readonly JsonSerializerOptions _plain = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Reads <paramref name="json"/> as a spec; an unknown member, a wrong type or bad JSON is refused.</summary>
    /// <param name="json">The document.</param>
    /// <returns>The spec, or SND-GRAPH-001 naming what is wrong.</returns>
    public static Result<GraphSpec> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        GraphSpec? spec;
        try
        {
            spec = JsonSerializer.Deserialize(json, GraphJsonContext.Default.GraphSpec);
        }
        catch (JsonException ex)
        {
            return SindriErrors.GraphInvalid.With(ex.Message);
        }

        if (spec is null || spec.Name is null || spec.Nodes is null)
        {
            return SindriErrors.GraphInvalid.With("a workflow graph needs a 'name' and a 'nodes' list");
        }

        return spec.Version == 1
            ? spec with { Edges = spec.Edges ?? [] }
            : SindriErrors.GraphInvalid.With($"version {spec.Version.ToString(CultureInfo.InvariantCulture)} is not a graph version this build reads; use 1");
    }

    /// <summary>The spec as JSON text, as a revision stores it.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The JSON.</returns>
    public static string Write(GraphSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return JsonSerializer.Serialize(spec, GraphJsonContext.Default.GraphSpec);
    }

    /// <summary>The spec's hash, 'sha256:' and 64 hex digits over its canonical JSON: the same graph hashes the same whatever its spacing or member order.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The hash.</returns>
    public static string Hash(GraphSpec spec)
    {
        var canonical = new StringBuilder();
        Canonical(canonical, JsonNode.Parse(Write(spec)));
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void Canonical(StringBuilder text, JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                _ = text.Append('{');
                var first = true;
                foreach (var (key, value) in obj.OrderBy(static p => p.Key, StringComparer.Ordinal))
                {
                    _ = first ? text : text.Append(',');
                    first = false;
                    _ = text.Append(JsonValue.Create(key).ToJsonString(_plain)).Append(':');
                    Canonical(text, value);
                }

                _ = text.Append('}');
                break;
            case JsonArray array:
                _ = text.Append('[');
                for (var i = 0; i < array.Count; i++)
                {
                    _ = i == 0 ? text : text.Append(',');
                    Canonical(text, array[i]);
                }

                _ = text.Append(']');
                break;
            default:
                _ = text.Append(node is null ? "null" : node.ToJsonString(_plain));
                break;
        }
    }
}
