using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Features.PollConnections;

namespace Aw.Sindri.Features.ListTriggerEvents;

/// <summary>Reads the recent events store for one kind: newest first, cut to the limit, each with its summary, trigger payload and filter check.</summary>
/// <param name="recent">The events seen on this box.</param>
internal sealed class ListTriggerEventsService(RecentEvents recent) : IService<ListTriggerEventsRequest, ListTriggerEventsResponse>
{
    /// <summary>How many events a list holds when the call names no limit (the trigger step shows 10).</summary>
    internal const int DefaultLimit = 10;

    public ValueTask<Result<ListTriggerEventsResponse>> Handle(ListTriggerEventsRequest request, CancellationToken cancellationToken)
    {
        _ = recent;
        throw new NotSupportedException("red: #3201");
    }
}
