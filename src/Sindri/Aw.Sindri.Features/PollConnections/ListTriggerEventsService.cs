using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Graph.Expressions;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>
/// Reads the recent events store for one kind: newest first, cut to the limit, each with a one-line summary, the trigger JSON a run would get and
/// whether a trigger with the asked filter takes it. An unknown or missing kind, a connection name of the wrong shape, a limit under 1 and a filter
/// that can't be read are invalid input (SND-CLI-003).
/// </summary>
/// <param name="recent">The events seen on this box.</param>
internal sealed class ListTriggerEventsService(RecentEvents recent) : IService<ListTriggerEventsRequest, ListTriggerEventsResponse>
{
    /// <summary>How many events a list holds when the call names no limit (the trigger step shows 10).</summary>
    internal const int DefaultLimit = 10;

    public ValueTask<Result<ListTriggerEventsResponse>> Handle(ListTriggerEventsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(List(request));
    }

    private Result<ListTriggerEventsResponse> List(ListTriggerEventsRequest request)
    {
        var kind = request.Kind is { Length: > 0 } key ? PollCatalog.Find(key) : null;
        if (kind is null)
        {
            return SindriErrors.InvalidInput.With($"'{request.Kind}' is not an event kind; 'sindri wf blocks' lists them");
        }

        var connection = Connection(request.Connection);
        if (connection.IsError)
        {
            return connection.Error;
        }

        var checkedInput = Check(request);
        if (checkedInput.IsError)
        {
            return checkedInput.Error;
        }

        var events = recent.Newest(kind.Value, connection.Value, request.Limit ?? DefaultLimit).Select(r => View(r, request.Filter)).ToList();
        return new ListTriggerEventsResponse(kind.Value.Key, events, Example: null);   // why: the example payload is the event catalog's sample (engine, #3201); null until it ships
    }

    private static Result<ConnectionName?> Connection(string? text)
    {
        if (text is null)
        {
            return Results.Success<ConnectionName?>(value: null);
        }

        var parsed = ConnectionName.Parse(text);
        return parsed.IsError ? SindriErrors.InvalidInput.With($"'{text}' is not a connection name like 'github:work'") : Results.Success<ConnectionName?>(parsed.Value);
    }

    private static Result<Unit> Check(ListTriggerEventsRequest request)
    {
        if (request.Limit is < 1)
        {
            return SindriErrors.InvalidInput.With("the limit is a number of 1 or more");
        }

        if (request.Filter is { } filter && !ConditionExpression.TryParse(filter, out _, out var error))
        {
            return SindriErrors.InvalidInput.With($"the filter can't be read: {error}");
        }

        return Unit.Value;
    }

    private static TriggerEventView View(RecentEvent seen, string? filter)
    {
        var polled = seen.Event;
        var payload = JsonNode.Parse(TriggerMatch.Trigger(seen.Connection, polled))!.AsObject();   // why: Trigger writes a JSON object
        return new TriggerEventView(polled.DeliveryKey, seen.Connection.ToString(), polled.OccurredAt, Summary(polled), payload, TriggerMatch.Wants(filter, seen.Connection, polled));
    }

    // 'acme/app#42 Fix login (ada)', 'acme/app refs/heads/main fed789 (eve)'; the kind when the event has none of these fields.
    private static string Summary(PolledEvent polled)
    {
        var fields = polled.Fields;
        var where = Joined(fields.GetValueOrDefault("repo"), "#", fields.GetValueOrDefault("number"));
        var what = First(fields, "title", "ref", "text");
        var who = First(fields, "author", "pusher", "user");
        var line = string.Join(' ', new[] { where, what, Short(fields.GetValueOrDefault("headSha"), what) }.Where(static p => p.Length > 0));
        line = line.Length == 0 ? polled.Kind.Key : line;
        return who.Length == 0 ? line : $"{line} ({who})";
    }

    // A push shows its head; a pull request's head stays out of the line, which already names the pull request.
    private static string Short(string? sha, string what) => sha is { Length: > 0 } && what.StartsWith("refs/", StringComparison.Ordinal) ? sha[..Math.Min(7, sha.Length)] : string.Empty;

    private static string Joined(string? first, string separator, string? second) =>
        (first ?? string.Empty) + (first is { Length: > 0 } && second is { Length: > 0 } ? separator + second : string.Empty);

    private static string First(IReadOnlyDictionary<string, string> fields, params string[] names) =>
        names.Select(n => fields.GetValueOrDefault(n)).FirstOrDefault(static v => v is { Length: > 0 }) ?? string.Empty;
}
