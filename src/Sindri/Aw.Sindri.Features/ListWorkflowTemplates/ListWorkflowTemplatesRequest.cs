using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListWorkflowTemplates;

/// <summary>List the workflow templates (GET /sindri/workflows/templates, 'sindri wf templates', MCP workflow_templates).</summary>
internal sealed record ListWorkflowTemplatesRequest : IQuery<ListWorkflowTemplatesResponse>;
