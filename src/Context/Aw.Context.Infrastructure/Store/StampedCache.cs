using Aw.Kernel;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Store;

/// <summary>
/// What a store loaded, kept by the change stamp (migration 0008) read in the same read transaction: while no write moved the stamp, the same
/// load is answered again without reading the rows (a 24k-node snapshot took a second to read on every map read). A store not yet migrated has no
/// stamp, and then nothing is kept.
/// </summary>
/// <typeparam name="T">What is kept.</typeparam>
internal sealed class StampedCache<T>
    where T : class
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (long Stamp, T Value)> _held = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Flight> _flights = new(StringComparer.Ordinal);

    /// <summary>The map's counter (migration 0008).</summary>
    internal const string MapStamp = "SELECT seq FROM change_stamp WHERE id = 1";

    /// <summary>The intent layer's counter (migration 0009).</summary>
    internal const string IntentStamp = "SELECT seq FROM stamp_intent WHERE id = 1";

    /// <summary>The counter <paramref name="query"/> reads, as of <paramref name="session"/>'s read transaction, or null when the store has none yet.</summary>
    internal static async ValueTask<long?> Stamp(SqliteReadSession session, string query, CancellationToken cancellationToken)
    {
        var stamp = await SqliteQueries.One(session, new SqlStatement(query), StampMapper.Instance, cancellationToken).ConfigureAwait(false);
        return stamp.IsSuccess ? stamp.Value : null;
    }

    /// <summary>What was kept under <paramref name="key"/> at <paramref name="stamp"/>, or null.</summary>
    internal T? Get(string key, long? stamp)
    {
        lock (_gate)
        {
            return stamp is { } now && _held.TryGetValue(key, out var held) && held.Stamp == now ? held.Value : null;
        }
    }

    /// <summary>
    /// What was kept under <paramref name="key"/> at <paramref name="stamp"/>, or <paramref name="load"/>'s result, run once however many callers miss together: the others await the
    /// same load. A load that fails, finds nothing or throws is not kept, so the next caller loads again. Without a stamp nothing is shared.
    /// </summary>
    internal async ValueTask<Result<T?>> GetOrLoad(string key, long? stamp, Func<Task<Result<T?>>> load, CancellationToken cancellationToken)
    {
        if (stamp is not { } now)
        {
            return await load().ConfigureAwait(false);
        }

        while (true)
        {
            if (Get(key, now) is { } held)
            {
                return held;
            }

            var flight = Join(key, now, load);
            try
            {
                var result = await flight.Load.Value.ConfigureAwait(false);
                Settle(key, flight, result);
                return result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // the first caller's token ended the shared load, not this caller's: load again
                Settle(key, flight, result: null);
            }
            catch
            {
                Settle(key, flight, result: null);
                throw;
            }
        }
    }

    private Flight Join(string key, long stamp, Func<Task<Result<T?>>> load)
    {
        lock (_gate)
        {
            if (_flights.TryGetValue(key, out var running) && running.Stamp == stamp)
            {
                return running;
            }

            var flight = new Flight(stamp, new Lazy<Task<Result<T?>>>(load, LazyThreadSafetyMode.ExecutionAndPublication));
            _flights[key] = flight;
            return flight;
        }
    }

    private void Settle(string key, Flight flight, Result<T?>? result)
    {
        if (result is { IsSuccess: true, Value: { } value })
        {
            Put(key, flight.Stamp, value);
        }

        lock (_gate)
        {
            if (_flights.TryGetValue(key, out var running) && ReferenceEquals(running, flight))
            {
                _ = _flights.Remove(key);
            }
        }
    }

    /// <summary>Keeps <paramref name="value"/> under <paramref name="key"/> at <paramref name="stamp"/> (nothing without a stamp).</summary>
    internal void Put(string key, long? stamp, T? value)
    {
        if (stamp is not { } now || value is null)
        {
            return;
        }

        lock (_gate)
        {
            // What was kept at another stamp can't be answered again: drop it, so at most the loads of one stamp are held.
            foreach (var stale in _held.Where(h => h.Value.Stamp != now).Select(h => h.Key).ToList())
            {
                _ = _held.Remove(stale);
            }

            _held[key] = (now, value);
        }
    }

    private sealed record Flight(long Stamp, Lazy<Task<Result<T?>>> Load);

    // why: Store sits below Graph (Graph's store reads through it), so the counter has its own mapper rather than Graph's (ARCH-CYCLE-001)
    private sealed class StampMapper : IRowMapper<long>
    {
        internal static readonly StampMapper Instance = new();

        public long Map(SqliteDataReader reader) => reader.GetInt64(0);
    }
}
