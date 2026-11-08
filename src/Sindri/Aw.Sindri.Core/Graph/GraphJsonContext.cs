using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Graph;

/// <summary>The source-generated JSON for the graph spec: camelCase names, optional members left out when null.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.Never, WriteIndented = false)]
[JsonSerializable(typeof(GraphSpec))]
internal sealed partial class GraphJsonContext : JsonSerializerContext;
