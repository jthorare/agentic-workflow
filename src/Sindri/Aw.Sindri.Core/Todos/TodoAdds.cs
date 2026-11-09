using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Todos;

/// <summary>
/// What an add must clear before it is written (todo spec §4.1, §4.4, §6): a retried client id answers with the todo it made, the 500 open todos cap, and the hourly limit for a session or a piped CLI caller.
/// The adds of one actor run inside <see cref="Lock"/>, so counting and appending never interleave.
/// </summary>
/// <param name="views">The todos, for the open count and a replayed add.</param>
/// <param name="ledger">The ledger the window reads.</param>
/// <param name="events">Reads the payloads.</param>
/// <param name="clock">The clock the hourly limit runs on.</param>
/// <param name="window">What the ledger says about earlier adds.</param>
public sealed class TodoAdds(IViewReader views, ILedgerStore ledger, EventFactory events, TimeProvider clock, TodoAddWindow window)
{
    // the cap is read from the views inside the per-actor lock, so two actors at 499 can both add (501): a small overshoot the spec accepts (§4.4)
    private const int MaxOpen = 500;
    private const int MaxPerHour = 20;

    /// <summary>The lock that serializes the adds of <paramref name="by"/>.</summary>
    /// <param name="by">Who writes.</param>
    /// <returns>The semaphore.</returns>
    public SemaphoreSlim Lock(string by) => window.Lock(by);

    /// <summary>Clears an add to be written.</summary>
    /// <param name="clientId">The caller's retry id, or null.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="by">Who writes ('by' of the event).</param>
    /// <param name="piped">Whether the request came with fence=1.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The todo a retried client id already made (nothing is written), null to go ahead, SND-TODO-006 or SND-TODO-429.</returns>
    public async ValueTask<Result<TodoAccepted?>> Admit(TodoClientId? clientId, CallerContext caller, string by, bool piped, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var now = clock.GetUtcNow();
        var seen = await window.Refresh(ledger, events, now, cancellationToken).ConfigureAwait(false);
        if (seen.IsError)
        {
            return seen.Error;
        }

        if (clientId is { } client && window.Find(by, client) is { } earlier)
        {
            return await Replay(earlier, caller, cancellationToken).ConfigureAwait(false);
        }

        var rows = await views.List<TodoRow>(ViewNames.Todo, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var open = rows.Value.Count(TodoDecisions.IsOpen);
        if (open >= MaxOpen)
        {
            return SindriErrors.TodoCap.With(SindriErrors.TodoCap.Title);
        }

        var limited = Limit(caller, by, piped, now);
        return limited is null ? Results.Success<TodoAccepted?>(value: null) : limited;
    }

    // a retried add answers with the todo the first one made, at the version it has now
    private async ValueTask<Result<TodoAccepted?>> Replay(TodoEarlierAdd earlier, CallerContext caller, CancellationToken cancellationToken)
    {
        var row = await views.Get<TodoRow>(ViewNames.Todo, earlier.Id.Value, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        return new TodoAccepted(new CommandAccepted(caller.CommandId, earlier.Sequence), earlier.Id, row.Value?.Version ?? 1, row.Value is { } current ? current.Due : earlier.Due);
    }

    // a session counts every add it made in the hour; a piped human call counts the piped ones; a terminal, the app and a service are never limited
    private Error? Limit(CallerContext caller, string by, bool piped, DateTimeOffset now)
    {
        var session = caller.Class == CallerClass.Session;
        if (!session && !(piped && caller.Class == CallerClass.Human))
        {
            return null;
        }

        var made = window.Within(by, pipeOnly: !session, now);
        if (made.Count < MaxPerHour)
        {
            return null;
        }

        var retryAt = made[0].At + TodoAddWindow.Span;
        var clockTime = TimeZoneInfo.ConvertTime(retryAt, clock.LocalTimeZone).ToString("HH:mm", CultureInfo.InvariantCulture);
        var message = session
            ? $"An agent can add {MaxPerHour} todos an hour. Try again after {clockTime}."
            : $"{MaxPerHour} adds an hour without a terminal. Try again after {clockTime}, or add from a terminal or the app.";
        using var at = JsonDocument.Parse($"\"{retryAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}\"");   // digits, dashes, colons, T and Z: nothing to escape
        return SindriErrors.TodoRateLimited.With(message).WithExtension("retryAfter", at.RootElement.Clone());
    }
}
