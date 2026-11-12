using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.ReadEvents;

/// <summary>Reads the event log by cursor; writes nothing.</summary>
/// <param name="events">The event log.</param>
internal sealed class ReadEventsService(IMetaEventStore events) : IService<ReadEventsRequest, ReadEventsResponse>
{
    public ValueTask<Result<ReadEventsResponse>> Handle(ReadEventsRequest request, CancellationToken cancellationToken)
    {
        _ = events;
        throw new NotSupportedException();
    }
}
