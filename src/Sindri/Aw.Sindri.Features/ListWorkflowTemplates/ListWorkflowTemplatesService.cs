using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.ListWorkflowTemplates;

/// <summary>Listing templates. A P0 contract stub (Workflows page spec §14): it refuses with SND-GRAPH-119 until its phase builds it.</summary>
internal sealed class ListWorkflowTemplatesService : IService<ListWorkflowTemplatesRequest, ListWorkflowTemplatesResponse>
{
    public ValueTask<Result<ListWorkflowTemplatesResponse>> Handle(ListWorkflowTemplatesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult<Result<ListWorkflowTemplatesResponse>>(SindriErrors.GraphNotBuilt.With("Listing templates is not built yet; the contract is in place"));
    }
}
