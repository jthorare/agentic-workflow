using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Graph.Templates;

namespace Aw.Sindri.Features.ListWorkflowTemplates;

/// <summary>Lists templates A, B and C for the start screen. 'LastRunAt' is null until a workflow made from a template records which one it came from.</summary>
internal sealed class ListWorkflowTemplatesService : IService<ListWorkflowTemplatesRequest, ListWorkflowTemplatesResponse>
{
    public ValueTask<Result<ListWorkflowTemplatesResponse>> Handle(ListWorkflowTemplatesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var views = StockTemplates.All
            .Select(static t => new WorkflowTemplateView(t.Key, t.Title, t.Trigger, t.Spec.Nodes.Count, t.Needs, LastRunAt: null))
            .ToList();
        return ValueTask.FromResult<Result<ListWorkflowTemplatesResponse>>(new ListWorkflowTemplatesResponse(views));
    }
}
