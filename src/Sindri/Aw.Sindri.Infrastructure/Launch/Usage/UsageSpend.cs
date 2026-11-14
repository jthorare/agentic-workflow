using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>
/// An agent's spend today (screens spec E-28): what the usage reader the catalog entry names counts for the local calendar day, priced by the table. An agent
/// whose entry names no reader has none (null: a budget on it is refused as 'no usage reader'). A reader nobody has checked against the real CLI counts tokens only.
/// The first read of a day is awaited; after it a caller always gets the last read at once, and a read older than a few seconds starts one refresh in the background (#2777).
/// </summary>
/// <param name="catalog">The catalog, which names each entry's reader.</param>
/// <param name="readers">The readers by name.</param>
/// <param name="clock">The time and the local zone.</param>
internal sealed class UsageSpend(IAgentCatalog catalog, IReadOnlyDictionary<string, IUsageReader> readers, TimeProvider clock) : ILaunchSpend, ILaunchUsage
{
    private static readonly TimeSpan _fresh = TimeSpan.FromSeconds(15);
    private readonly ConcurrentDictionary<AgentId, (DateTimeOffset At, DateOnly Day, AgentSpend Spend)> _read = new();
    private readonly Lock _gate = new();
    private readonly Dictionary<AgentId, (DateOnly Day, Task<AgentSpend> Task)> _flights = [];

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ModelTokens>?> OfSession(AgentId target, SessionId session, CancellationToken cancellationToken) =>
        catalog.Resolve(target)?.Usage is { } name && readers.TryGetValue(name, out var reader) ? await reader.OfSession(session, cancellationToken).ConfigureAwait(false) : null;

    /// <inheritdoc/>
    public SpendPeek Peek(AgentId target)
    {
        if (catalog.Resolve(target)?.Usage is not { } name || !readers.TryGetValue(name, out var reader))
        {
            return new SpendPeek(Landed: true, Spend: null);
        }

        var now = clock.GetUtcNow();
        var window = LocalDay(now, clock.LocalTimeZone);
        if (_read.TryGetValue(target, out var cached) && cached.Day == window.Day)
        {
            if (now - cached.At >= _fresh)
            {
                Background(Flight(target, reader, window));
            }

            return new SpendPeek(Landed: true, cached.Spend);
        }

        // the day's first read: start (or join) the shared one and answer at once; the caller is not told its value
        Background(Flight(target, reader, window));
        return new SpendPeek(Landed: false, Spend: null);
    }

    private static void Background(Task<AgentSpend> read) =>
        _ = read.ContinueWith(static t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    /// <inheritdoc/>
    public async ValueTask<Result<AgentSpend?>> Today(AgentId target, CancellationToken cancellationToken)
    {
        if (catalog.Resolve(target)?.Usage is not { } name || !readers.TryGetValue(name, out var reader))
        {
            return Results.Success(default(AgentSpend));
        }

        var now = clock.GetUtcNow();
        var (start, end, day) = LocalDay(now, clock.LocalTimeZone);
        if (_read.TryGetValue(target, out var cached) && cached.Day == day)
        {
            if (now - cached.At >= _fresh)
            {
                // stale: answer from the last read and refresh once in the background (#2777); a failed refresh keeps the value and the next stale call retries
                _ = Flight(target, reader, (start, end, day)).ContinueWith(static t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            return Results.Success<AgentSpend?>(cached.Spend);
        }

        // nothing read yet for this local day: wait for the one shared read (the caller's token stops its wait, not the read)
        return Results.Success<AgentSpend?>(await Flight(target, reader, (start, end, day)).WaitAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The read in flight for <paramref name="target"/>'s day, started when there is none; every caller of one day shares it. The read has begun (the reader was called) when this returns.</summary>
    private Task<AgentSpend> Flight(AgentId target, IUsageReader reader, (DateTimeOffset Start, DateTimeOffset End, DateOnly Day) window)
    {
        TaskCompletionSource<AgentSpend> source;
        lock (_gate)
        {
            if (_flights.TryGetValue(target, out var flight) && flight.Day == window.Day)
            {
                return flight.Task;
            }

            source = new TaskCompletionSource<AgentSpend>(TaskCreationOptions.RunContinuationsAsynchronously);
            _flights[target] = (window.Day, source.Task);
        }

        _ = Fill(source, target, reader, window);
        return source.Task;
    }

    private async Task Fill(TaskCompletionSource<AgentSpend> source, AgentId target, IUsageReader reader, (DateTimeOffset Start, DateTimeOffset End, DateOnly Day) window)
    {
        try
        {
            var used = Budgets.Spend(await reader.Between(window.Start, window.End, CancellationToken.None).ConfigureAwait(false));
            var spend = reader.Verified ? used : new AgentSpend(0m, used.Tokens, SpendBasis.TokensOnly);
            _read[target] = (clock.GetUtcNow(), window.Day, spend);
            _ = source.TrySetResult(spend);
        }
        catch (Exception e)
        {
            _ = source.TrySetException(e);
        }
        finally
        {
            lock (_gate)
            {
                if (_flights.TryGetValue(target, out var flight) && ReferenceEquals(flight.Task, source.Task))
                {
                    _ = _flights.Remove(target);
                }
            }
        }
    }

    /// <summary>The local calendar day that holds <paramref name="now"/>, as the instants it starts and ends.</summary>
    private static (DateTimeOffset Start, DateTimeOffset End, DateOnly Day) LocalDay(DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(now, zone);
        return (Midnight(local.Date, zone), Midnight(local.Date.AddDays(1), zone), DateOnly.FromDateTime(local.Date));
    }

    private static DateTimeOffset Midnight(DateTime date, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, zone.IsInvalidTime(unspecified) ? zone.GetUtcOffset(unspecified.AddHours(1)) : zone.GetUtcOffset(unspecified));
    }
}
