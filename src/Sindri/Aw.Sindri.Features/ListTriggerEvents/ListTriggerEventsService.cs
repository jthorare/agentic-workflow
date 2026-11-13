using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.ListTriggerEvents;

/// <summary>Listing trigger events. A P0 contract stub (Workflows page spec §14): it refuses with SND-GRAPH-119 until its phase builds it.</summary>
internal sealed class ListTriggerEventsService : IService<ListTriggerEventsRequest, ListTriggerEventsResponse>
{
    public ValueTask<Result<ListTriggerEventsResponse>> Handle(ListTriggerEventsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult<Result<ListTriggerEventsResponse>>(SindriErrors.GraphNotBuilt.With("Listing trigger events is not built yet; the contract is in place"));
    }
}
