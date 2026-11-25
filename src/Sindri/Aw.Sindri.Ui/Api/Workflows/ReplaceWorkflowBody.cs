using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>PUT /sindri/workflows/{slug}'s body: the whole graph, saved as a new revision when the base is still the newest.</summary>
/// <param name="BaseRevision">The newest revision the editor read.</param>
/// <param name="Spec">The graph to save, kept raw.</param>
internal sealed record ReplaceWorkflowBody(int BaseRevision, JsonElement Spec);
