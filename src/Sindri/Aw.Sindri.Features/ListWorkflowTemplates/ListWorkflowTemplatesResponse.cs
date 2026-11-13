namespace Aw.Sindri.Features.ListWorkflowTemplates;

/// <summary>The templates.</summary>
/// <param name="Templates">The templates, in the order the start screen shows them.</param>
internal sealed record ListWorkflowTemplatesResponse(IReadOnlyList<WorkflowTemplateView> Templates);
