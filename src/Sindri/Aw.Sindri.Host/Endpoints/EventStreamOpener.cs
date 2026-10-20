using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Host.Server;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/events: the ledger's public events as a server-sent event stream (API spec §5; factory addendum §17.21). Everything
/// that can refuse the request is decided before the first frame, as a ProblemDetails answer: a bad filter (400), a cursor past the
/// ledger's head (400), an item filter that names no item the caller may see (404) and no free stream slot (429, SND-API-012). The cursor is the Last-Event-ID header when it holds a sequence,
/// else ?after=, else 0 (C4); a stream of only 'workflow.instance-changed' with no explicit cursor (no valid Last-Event-ID, no non-empty ?after=) opens at the ledger head instead (StartAt). ?item= (an id or tracker alias), ?types= (globs) and ?instance= (engine spec S1-D6) filter the stream.
/// </summary>
/// <param name="ledger">The ledger, for the head.</param>
/// <param name="slots">The open streams.</param>
/// <param name="items">Resolves a filter item named by alias.</param>
/// <param name="repos">Resolves a filter item named by id, from the ledger (never the views).</param>
/// <param name="callers">Who opens the stream.</param>
/// <param name="shutdown">Ends open streams on stop.</param>
internal sealed class EventStreamOpener(ILedgerReader ledger, IStreamSlots slots, IItemResolver items, IItemRepos repos, ICallerContextAccessor callers, IShutdown shutdown)
{
    /// <summary>The answer for <paramref name="context"/>'s request: an open stream or the refusal.</summary>
    /// <param name="context">The request.</param>
    /// <param name="service">Reads each page.</param>
    /// <param name="pump">The loop the answer runs.</param>
    /// <returns>The stream, or a ProblemDetails answer.</returns>
    public async Task<IResult> Open(HttpContext context, IService<StreamEventsRequest, StreamEventsResponse> service, EventPump pump)
    {
        ArgumentNullException.ThrowIfNull(context);
        var request = Bind(context.Request);
        if (request.IsError)
        {
            return Refused(request.Error, StatusCodes.Status400BadRequest);
        }

        var head = await ledger.Head(context.RequestAborted).ConfigureAwait(false);
        if (head.IsError)
        {
            return Refused(head.Error, StatusOf(head.Error.Kind));
        }

        var bound = request.Value with { After = StartAt(context.Request, request.Value, head.Value) };

        if (bound.After > head.Value)
        {
            return Refused(
                SindriErrors.InvalidInput.With(string.Create(CultureInfo.InvariantCulture,
                    $"the cursor {bound.After} is past the ledger's head {head.Value} (fix: after={head.Value}, or leave it out)")),
                StatusCodes.Status400BadRequest);
        }

        var item = await ItemFilters.Resolve(bound.Ref, items, repos, callers, context.RequestAborted).ConfigureAwait(false);
        if (item.IsError)
        {
            return Refused(item.Error, StatusOf(item.Error.Kind));   // 404 SND-ITEM-404 for an item that isn't there or that a session may not see
        }

        var caller = callers.Current?.Class ?? CallerClass.Human;
        if (!slots.TryEnter(caller, out var slot))
        {
            return Refused(SindriErrors.StreamsFull.With(SindriErrors.StreamsFull.Title), StatusCodes.Status429TooManyRequests);
        }

        StreamTelemetry.Opened(caller, slots.Open(caller));
        var resolved = bound with { Ref = item.Value is { } id ? ItemRef.To(id) : null };   // the pages resolve it again, by id, from the ledger
        return new SseResult(pump, service, resolved, new Counted(slot, slots, caller), shutdown.Stopping);
    }

    /// <summary>
    /// Where the stream starts. A stream of only 'workflow.instance-changed' sends no "id:" line, so an auto-reconnect names no cursor and
    /// would replay the whole history as frames: with no explicit cursor (no Last-Event-ID, no ?after=) it opens at the head instead.
    /// </summary>
    internal static long StartAt(HttpRequest http, StreamEventsRequest bound, long head)
    {
        var derivedOnly = bound.Types is { } types && types.Globs.All(static g => string.Equals(g, InstanceChanged.Type, StringComparison.Ordinal));
        var explicitCursor = (long.TryParse(http.Headers["Last-Event-ID"], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var last) && last >= 0)
            || !string.IsNullOrEmpty(http.Query["after"]);
        return derivedOnly && !explicitCursor ? head : bound.After;
    }

    private static int StatusOf(ErrorKind kind)
    {
        if (kind == ErrorKind.Invalid)
        {
            return StatusCodes.Status400BadRequest;
        }

        if (kind == ErrorKind.NotFound)
        {
            return StatusCodes.Status404NotFound;
        }

        return kind == ErrorKind.Unavailable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
    }

    /// <summary>A ProblemDetails answer, as every other route sends one: title the code, detail the message.</summary>
    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Refused(Error error, int status) => TypedResults.Problem(title: error.Code, detail: error.Message, statusCode: status);

    /// <summary>The filters and first cursor a request names, or the 400 for a malformed one.</summary>
    internal static Result<StreamEventsRequest> Bind(HttpRequest http)
    {
        var item = ItemRoute.ItemFilter(http.Query["item"]);
        var types = TypeFilterOf(http.Query["types"]);
        var instance = InstanceOf(http.Query["instance"]);
        var after = Cursor(http.Headers["Last-Event-ID"], http.Query["after"]);
        if (item.IsError || types.IsError || instance.IsError || after.IsError)
        {
            return item.IsError ? item.Error : types.IsError ? types.Error : instance.IsError ? instance.Error : after.Error;
        }

        return new StreamEventsRequest(after.Value, Item: null, Instance: instance.Value, Types: types.Value, Ref: item.Value);
    }

    /// <summary>The first cursor (C4): Last-Event-ID when it holds a sequence (0 or more), else ?after=, else 0.</summary>
    private static Result<long> Cursor(string? lastEventId, string? after)
    {
        if (long.TryParse(lastEventId, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var last) && last >= 0)
        {
            return last;   // a negative one is no sequence a stream ever sent: it falls through like an unparseable header, and after= is judged
        }

        return after is null || after.Length == 0
            ? 0L
            : long.TryParse(after, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var cursor)
                ? NonNegative(cursor)
                : SindriErrors.InvalidInput.With("after must be a ledger sequence, 0 or more (fix: ?after=0)");
    }

    private static Result<long> NonNegative(long cursor) => cursor >= 0
        ? cursor
        : SindriErrors.InvalidInput.With(string.Create(CultureInfo.InvariantCulture, $"after {cursor} is negative; a cursor is 0 or more (fix: ?after=0, or leave it out)"));

    private static Result<TypeFilter?> TypeFilterOf(string? types)
    {
        if (string.IsNullOrEmpty(types))
        {
            return (TypeFilter?)null;
        }

        var parsed = TypeFilter.Parse(types);
        return parsed.IsError ? parsed.Error : parsed.Value;
    }

    private static Result<InstanceAddress?> InstanceOf(string? instance)
    {
        if (string.IsNullOrEmpty(instance))
        {
            return (InstanceAddress?)null;
        }

        return InstanceAddress.TryFrom(instance, out var address)
            ? address
            : SindriErrors.InvalidInput.With($"'{SafeText.Line(SafeText.Escape(instance), 80)}' isn't an instance address (fix: ?instance=T-1/step/1)");
    }

    /// <summary>The slot, with the open-streams gauge updated when it is freed.</summary>
    private sealed class Counted(IDisposable inner, IStreamSlots slots, CallerClass caller) : IDisposable
    {
        public void Dispose()
        {
            inner.Dispose();
            StreamTelemetry.Opened(caller, slots.Open(caller));
        }
    }
}
