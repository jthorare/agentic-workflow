using System.Text.Json.Nodes;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/workflows.</summary>
/// <param name="Slug">The new workflow's slug.</param>
/// <param name="Template">A starter template's name, or null.</param>
/// <param name="Spec">The graph as a JSON object, or null.</param>
internal sealed record CreateWorkflowBody(string? Slug = null, string? Template = null, JsonNode? Spec = null);
