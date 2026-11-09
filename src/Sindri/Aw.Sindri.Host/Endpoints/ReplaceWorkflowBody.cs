using System.Text.Json.Nodes;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of PUT /sindri/workflows/{slug}.</summary>
/// <param name="BaseRevision">The latest revision the caller read.</param>
/// <param name="Spec">The whole graph as a JSON object.</param>
internal sealed record ReplaceWorkflowBody(int? BaseRevision, JsonNode? Spec);
