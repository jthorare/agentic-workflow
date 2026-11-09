using System.Text.Json.Nodes;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/workflows/{slug}/ops.</summary>
/// <param name="BaseRevision">The latest revision the caller read.</param>
/// <param name="Ops">The ops, a JSON array of objects with an 'op' member.</param>
internal sealed record ApplyWorkflowOpsBody(int? BaseRevision, JsonNode? Ops);
