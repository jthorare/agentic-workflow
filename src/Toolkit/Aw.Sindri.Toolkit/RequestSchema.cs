using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace Aw.Sindri.Toolkit;

/// <summary>A tool request's JSON schema, exported from its source-generated type info, with nullable members optional.</summary>
internal sealed class RequestSchema
{
    // why: the root type has no nullability annotation, so the exporter would type it ["object", "null"]; MCP wants an input schema of type "object"
    private static readonly JsonSchemaExporterOptions _exporter = new() { TreatNullObliviousAsNonNullable = true };

    private RequestSchema(JsonObject schema, IReadOnlyList<string> optional)
    {
        Schema = schema;
        Optional = optional;
    }

    /// <summary>The schema.</summary>
    public JsonObject Schema { get; }

    /// <summary>The names of the members a client may leave out.</summary>
    public IReadOnlyList<string> Optional { get; }

    /// <summary>Exports the schema of <paramref name="typeInfo"/>.</summary>
    /// <param name="typeInfo">A type info from the toolkit's JSON context.</param>
    /// <param name="markNullableOptional">True for a request: a member whose schema allows null leaves the required list.</param>
    /// <returns>The schema and the optional member names.</returns>
    public static RequestSchema Export(JsonTypeInfo typeInfo, bool markNullableOptional)
    {
        var options = typeInfo.Options;
        if (!options.RespectNullableAnnotations || !options.RespectRequiredConstructorParameters)
        {
            throw new ToolkitException(
                ToolkitErrors.InvalidDeclaration,
                $"The JSON context of {typeInfo.Type.Name} must set RespectNullableAnnotations and RespectRequiredConstructorParameters: add them to its [JsonSourceGenerationOptions].");
        }

        if (JsonSchemaExporter.GetJsonSchemaAsNode(typeInfo, _exporter) is not JsonObject schema
            || !(schema["type"] is JsonValue kind && kind.TryGetValue<string>(out var text) && string.Equals(text, "object", StringComparison.Ordinal)))
        {
            throw new ToolkitException(ToolkitErrors.InvalidDeclaration, $"{typeInfo.Type.Name} has no object schema: use a record.");
        }

        var optional = markNullableOptional ? NullableMembers(schema) : [];
        DropFromRequired(schema, optional);
        return new RequestSchema(schema, optional);
    }

    private static List<string> NullableMembers(JsonObject schema) =>
        schema["properties"] is JsonObject properties ? [.. properties.Where(p => AllowsNull(p.Value)).Select(p => p.Key)] : [];

    private static void DropFromRequired(JsonObject schema, List<string> optional)
    {
        if (optional.Count == 0 || schema["required"] is not JsonArray required)
        {
            return;
        }

        for (var i = required.Count - 1; i >= 0; i--)
        {
            if (required[i] is JsonValue name && optional.Contains(name.GetValue<string>(), StringComparer.Ordinal))
            {
                required.RemoveAt(i);
            }
        }

        if (required.Count == 0)
        {
            _ = schema.Remove("required");
        }
    }

    private static bool AllowsNull(JsonNode? property) =>
        property is JsonObject schema
        && schema["type"] is JsonArray types
        && types.Any(t => t is JsonValue v && v.TryGetValue<string>(out var text) && string.Equals(text, "null", StringComparison.Ordinal));
}
