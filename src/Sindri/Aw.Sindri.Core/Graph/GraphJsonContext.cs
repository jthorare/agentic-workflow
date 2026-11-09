using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Graph;

/// <summary>The source-generated JSON for the graph spec: camelCase names; an optional member is left out when null.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
[JsonSerializable(typeof(GraphSpec))]
internal sealed partial class GraphJsonContext : JsonSerializerContext;
