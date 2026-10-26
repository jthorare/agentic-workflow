using System.Text;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.Scoping;

/// <summary>The scoping station's schemas (schemas/scoping), embedded in this assembly, and the validators over them.</summary>
internal static class ScopeSchemas
{
    private static readonly Dictionary<string, JsonNode> _documents = new(StringComparer.Ordinal);
    private static readonly Lock _gate = new();

    /// <summary>The errors of a fact against its schema's fact form ('$defs/fact', for the schemas that have both forms), or against the schema itself.</summary>
    /// <param name="schema">The schema's file name without '.json' ('scope-map', 'acceptance').</param>
    /// <param name="fact">The fact's tree.</param>
    /// <param name="packetFile">True to check the packet-file form ('$defs/file') instead of the fact form.</param>
    internal static IReadOnlyList<(string Path, string Message)> Errors(string schema, JsonNode? fact, bool packetFile = false)
    {
        var document = Document(schema);
        var form = (document["$defs"] as JsonObject)?[packetFile ? "file" : "fact"];
        return new JsonSchemaLite(document).Validate(fact, form ?? document);
    }

    private static JsonNode Document(string name)
    {
        lock (_gate)
        {
            if (!_documents.TryGetValue(name, out var document))
            {
                using var stream = typeof(ScopeSchemas).Assembly.GetManifestResourceStream($"scoping-schemas/{name}.json")
                    ?? throw new InvalidOperationException($"The scoping schema '{name}' is not embedded (schemas/scoping/{name}.json).");
                using var reader = new StreamReader(stream, Encoding.UTF8);
                document = JsonNode.Parse(reader.ReadToEnd()) ?? new JsonObject();
                _documents[name] = document;
            }

            return document;
        }
    }

    /// <summary>The seeds the intake Step writes: each fact's empty value (schemas/scoping/seeds.json), by key without the 'fact:' prefix.</summary>
    internal static IReadOnlyDictionary<string, JsonNode> Seeds() =>
        ((JsonObject)Document("seeds")).ToDictionary(
            static p => p.Key["fact:".Length..],
            static p => p.Value?.DeepClone() ?? new JsonObject(),
            StringComparer.Ordinal);
}
