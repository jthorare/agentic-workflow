using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.ReadEvents;

/// <summary>Reads the event log by cursor; writes nothing.</summary>
/// <param name="events">The event log.</param>
internal sealed class ReadEventsService(IMetaEventStore events) : IService<ReadEventsRequest, ReadEventsResponse>
{
    public async ValueTask<Result<ReadEventsResponse>> Handle(ReadEventsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var read = await events.Read(request.After, request.Kind, request.Lane, request.Max, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var lines = new List<EventLine>(read.Value.Events.Count);
        foreach (var e in read.Value.Events)
        {
            using var fields = JsonDocument.Parse(e.Fields);
            lines.Add(new EventLine(e.Id, e.At, e.Kind, e.Lane, fields.RootElement.Clone()));
        }

        return new ReadEventsResponse(lines, lines.Count == 0 ? request.After : lines[^1].Id, read.Value.Oldest, read.Value.Latest, request.After + 1 < read.Value.Oldest);
    }
}
