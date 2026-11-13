using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>The body of POST /sindri/workflows/validate. Give a stored workflow (slug, and a revision or the newest) or a graph.</summary>
/// <param name="Slug">A stored workflow's slug, or null.</param>
/// <param name="Revision">Its revision, or null for the newest.</param>
/// <param name="Spec">A graph as a JSON object, or null.</param>
internal sealed record ValidateWorkflowBody(string? Slug, int? Revision, JsonElement? Spec);
