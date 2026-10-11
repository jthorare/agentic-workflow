using System.Net.ServerSentEvents;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Features.StreamEvents;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/events: the ledger as a server-sent event stream (factory addendum §17.21). A client resumes from its
/// own cursor with ?after=N or the Last-Event-ID header; the stream polls the ledger once a second. It ends cleanly when the
/// client goes away or a stop is requested, during a read or a wait, so a stop doesn't wait out Kestrel's graceful-shutdown timeout.
/// </summary>
internal static class EventStream
{
    internal static readonly TimeSpan Poll = TimeSpan.FromSeconds(1);

    internal static IResult Stream(HttpContext context, IService<StreamEventsRequest, StreamEventsResponse> service, TimeProvider clock, IShutdown shutdown, long? after)
    {
        var cursor = after ?? (long.TryParse(context.Request.Headers["Last-Event-ID"], out var last) ? last : 0);
        return TypedResults.ServerSentEvents(Events(service, clock, cursor, (context.RequestAborted, shutdown.Stopping)));
    }

    internal static async IAsyncEnumerable<SseItem<EventView>> Events(
        IService<StreamEventsRequest, StreamEventsResponse> service, TimeProvider clock, long cursor, (CancellationToken Aborted, CancellationToken Stopping) until)
    {
        using var either = CancellationTokenSource.CreateLinkedTokenSource(until.Aborted, until.Stopping);
        var ct = either.Token;
        while (!ct.IsCancellationRequested)
        {
            Result<StreamEventsResponse> page;
            try
            {
                page = await service.Handle(new StreamEventsRequest(cursor), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                yield break;   // the client left or a stop came while the ledger was read
            }

            foreach (var e in page.IsSuccess ? page.Value.Events : [])
            {
                cursor = e.Sequence;
                yield return new SseItem<EventView>(e, e.Type) { EventId = e.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            }

            try
            {
                await Task.Delay(Poll, clock, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }
}
