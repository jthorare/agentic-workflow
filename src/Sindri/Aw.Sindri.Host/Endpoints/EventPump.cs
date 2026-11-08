using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Features.StreamEvents;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The event stream's loop (API spec §5.2, §5.4, §5.5): read a page after the cursor, write each event as a frame, and when caught
/// up wait on the ledger signal (a committed append wakes it at once; the one-second poll is the fallback), writing a ': ping'
/// comment after 15 seconds without a write. A full page, or a cursor still behind the head, reads again at once. A read error is
/// sent as an 'event: error' frame and ends the stream; so does an unexpected exception (logged, told as SND-API-005 without its text);
/// the client leaving or a stop ends it without a frame.
/// </summary>
/// <param name="ledger">The ledger, for its head.</param>
/// <param name="signal">Wakes the wait when an append commits.</param>
/// <param name="clock">Times the idle ping and the wait.</param>
/// <param name="json">The package's AwJson options, for the frames' data.</param>
/// <param name="log">Where a row sent without a readable payload is logged.</param>
internal sealed partial class EventPump(ILedgerReader ledger, ILedgerSignal signal, TimeProvider clock, JsonSerializerOptions json, ILogger<EventPump> log)
{
    internal static readonly TimeSpan Poll = TimeSpan.FromSeconds(1);

    internal static readonly TimeSpan PingEvery = TimeSpan.FromSeconds(15);

    /// <summary>Streams from <paramref name="request"/>'s cursor to <paramref name="writer"/> until <paramref name="cancellationToken"/> is cancelled, the client is gone or a read fails.</summary>
    /// <param name="writer">Where the frames go.</param>
    /// <param name="service">Reads each page.</param>
    /// <param name="request">The filters and the first cursor (its After).</param>
    /// <param name="cancellationToken">Ends the stream: the client left or a stop was requested.</param>
    /// <returns>When the stream has ended.</returns>
    public async Task Run(SseWriter writer, IService<StreamEventsRequest, StreamEventsResponse> service, StreamEventsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await Loop(writer, service, request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // the client left or a stop came, during a read, a wait or a write: the stream ends cleanly
        }
        catch (IOException)
        {
            // the client's connection broke mid-write
        }
        catch (Exception e)
        {
            // a bug: the stream is the boundary, so say so to the client and the log and end it, instead of letting the host abort the connection with no word
            LogFailed(log, e);
            await TellInternalError(writer).ConfigureAwait(false);
        }
    }

    private async Task Loop(SseWriter writer, IService<StreamEventsRequest, StreamEventsResponse> service, StreamEventsRequest request, CancellationToken cancellationToken)
    {
        var cursor = request.After;
        var lastWrite = clock.GetTimestamp();
        var scan = ScanOf(request, out var derived);
        while (!cancellationToken.IsCancellationRequested)
        {
            var next = await NextPage(service, scan with { After = cursor }, cancellationToken).ConfigureAwait(false);
            if (next.IsError)
            {
                await writer.Event(id: null, "error", Text(new StreamErrorFrame(next.Error.Code, next.Error.Message)), cancellationToken).ConfigureAwait(false);
                return;
            }

            var (page, head) = next.Value;
            foreach (var e in page.Events.Where(e => request.Types?.Matches(e.Type) ?? true))
            {
                await Send(writer, e, cancellationToken).ConfigureAwait(false);
                lastWrite = clock.GetTimestamp();
            }

            if (derived)
            {
                foreach (var changed in Changed(page.Events))
                {
                    await writer.Event(id: null, InstanceChanged.Type, Text(changed), cancellationToken).ConfigureAwait(false);
                    lastWrite = clock.GetTimestamp();
                }
            }

            cursor = Math.Max(cursor, page.ScannedTo);
            if (page.Events.Count < request.Max && cursor >= head)
            {
                lastWrite = await WaitOrPing(writer, cursor, lastWrite, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The read a request that names 'workflow.instance-changed' makes: that type is no ledger row, so its glob is swapped for the 'workflow.*'
    /// rows it is derived from, and the loop sends a raw row only when the caller's own types match it. A request that doesn't name it reads as asked.
    /// </summary>
    private static StreamEventsRequest ScanOf(StreamEventsRequest request, out bool derived)
    {
        derived = request.Types?.Globs.Contains(InstanceChanged.Type, StringComparer.Ordinal) ?? false;
        if (request.Types is not { } types || !derived)
        {
            return request;
        }

        var globs = types.Globs.Select(g => string.Equals(g, InstanceChanged.Type, StringComparison.Ordinal) ? "workflow.*" : g).Distinct(StringComparer.Ordinal);
        return request with { Types = TypeFilter.Parse(string.Join(',', globs)).Value };
    }

    /// <summary>One change per instance in <paramref name="page"/>, at its newest row, oldest first: a burst, or a replay from an old cursor, is one frame to re-query on.</summary>
    private static IEnumerable<InstanceChanged> Changed(IReadOnlyList<PublicEvent> page) =>
        page.Select(InstanceChanged.From).OfType<InstanceChanged>()
            .GroupBy(static c => c.Instance, StringComparer.Ordinal).Select(static g => g.Last()).OrderBy(static c => c.Cursor);

    /// <summary>Best effort: the client may be gone, and the stop that ended the stream may have fired.</summary>
    private async ValueTask TellInternalError(SseWriter writer)
    {
        try
        {
            await writer.Event(id: null, "error", Text(new StreamErrorFrame(SindriErrors.ApiInternal.Code, SindriErrors.ApiInternal.Title)), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // nobody to tell
        }
    }

    private async ValueTask Send(SseWriter writer, PublicEvent e, CancellationToken cancellationToken)
    {
        if (e.Payload.ValueKind == JsonValueKind.Null)
        {
            LogUnreadable(log, e.Sequence, e.Type);   // each row is sent once per stream, so this is once per row
        }

        await writer.Event(e.Sequence, e.Type, Text(e), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<(StreamEventsResponse Page, long Head)>> NextPage(IService<StreamEventsRequest, StreamEventsResponse> service, StreamEventsRequest request, CancellationToken cancellationToken)
    {
        var page = await service.Handle(request, cancellationToken).ConfigureAwait(false);
        if (page.IsError)
        {
            return page.Error;
        }

        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);   // after the page: an append in between only means another read at once
        return head.IsError ? head.Error : (page.Value, head.Value);
    }

    /// <summary>Waits for a pulse past <paramref name="cursor"/> for at most a second, or writes the ping when the stream has been idle that long; returns the time of the last write.</summary>
    private async ValueTask<long> WaitOrPing(SseWriter writer, long cursor, long lastWrite, CancellationToken cancellationToken)
    {
        var untilPing = PingEvery - clock.GetElapsedTime(lastWrite);
        if (untilPing <= TimeSpan.Zero)
        {
            await writer.Comment("ping", cancellationToken).ConfigureAwait(false);
            return clock.GetTimestamp();
        }

        _ = await signal.WaitAfter(cursor, untilPing < Poll ? untilPing : Poll, cancellationToken).ConfigureAwait(false);
        return lastWrite;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The event stream ended with an unexpected error (SND-API-005)")]
    private static partial void LogFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Event {Sequence} ({Type}) was sent with payload null: its stored payload doesn't read as the public record")]
    private static partial void LogUnreadable(ILogger logger, long sequence, string type);

    private string Text<T>(T value) => Encoding.UTF8.GetString(AwJson.Serialize(value, json));
}
