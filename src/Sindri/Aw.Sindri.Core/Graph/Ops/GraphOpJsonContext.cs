using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>
/// The source-generated JSON for a list of graph ops: camelCase names, and the "op" member may come anywhere in the object.
/// It lives here and not in <see cref="GraphJsonContext"/> because the graph namespace must not depend on its ops (ARCH-CYCLE-001: ops use the validator, which uses the graph).
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false, AllowOutOfOrderMetadataProperties = true)]
[JsonSerializable(typeof(IReadOnlyList<GraphOp>))]
internal sealed partial class GraphOpJsonContext : JsonSerializerContext;
