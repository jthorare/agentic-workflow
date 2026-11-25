namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>GET /sindri/workflows/templates: the templates, in the order the start screen shows them.</summary>
/// <param name="Templates">The templates.</param>
internal sealed record WorkflowTemplateList(IReadOnlyList<WorkflowTemplateView> Templates);
