using System.Collections.Concurrent;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Launches;

/// <summary>
/// The last totals sampled per launch and the last sample number used, in memory. A restart samples once more, which is harmless:
/// the number is never lower than the clock's and never repeats within a run, so no sample is dropped as a duplicate key. Only "within a run" holds: after a restart the floor starts again from the clock and can equal an earlier bumped number, which drops one sample as already recorded.
/// </summary>
public sealed class UsageSamples
{
    /// <summary>How many launches' totals are kept before the ended ones are forgotten.</summary>
    public const int Retained = 256;

    private readonly Lock _gate = new();
    private readonly Dictionary<LaunchId, (long Input, long Output, int Number)> _last = [];
    private readonly ConcurrentDictionary<LaunchId, SemaphoreSlim> _turns = new();

    /// <summary>Takes the launch's turn: a tick and a flush of one launch run their whole read, number, append and record one after the other, so the newer totals are the ones the ledger keeps.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The turn; disposing it hands the launch to the next caller.</returns>
    public async ValueTask<IDisposable> Turn(LaunchId launch, CancellationToken cancellationToken)
    {
        var turn = _turns.GetOrAdd(launch, static _ => new SemaphoreSlim(1, 1));
        await turn.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Release(turn);
    }

    private sealed class Release(SemaphoreSlim turn) : IDisposable
    {
        public void Dispose() => _ = turn.Release();
    }

    /// <summary>Whether <paramref name="input"/> and <paramref name="output"/> differ from the totals last recorded for the launch.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="input">Input tokens so far.</param>
    /// <param name="output">Output tokens so far.</param>
    /// <returns>True when the totals are new.</returns>
    public bool IsNew(LaunchId launch, long input, long output)
    {
        lock (_gate)
        {
            return !_last.TryGetValue(launch, out var seen) || (seen.Input, seen.Output) != (input, output);
        }
    }

    /// <summary>The number for the launch's next sample: <paramref name="floor"/> (from the clock), or one past the last used when that is not higher.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="floor">The clock-derived number.</param>
    /// <returns>The number.</returns>
    public int Next(LaunchId launch, int floor)
    {
        lock (_gate)
        {
            return _last.TryGetValue(launch, out var seen) ? Math.Max(floor, seen.Number + 1) : floor;
        }
    }

    /// <summary>Notes that a sample with these totals and this number was appended. Call it only after the append succeeded.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="input">Input tokens so far.</param>
    /// <param name="output">Output tokens so far.</param>
    /// <param name="number">The sample's number.</param>
    public void Record(LaunchId launch, long input, long output, int number)
    {
        lock (_gate)
        {
            _last[launch] = (input, output, number);
        }
    }

    /// <summary>
    /// Forgets the launches not in <paramref name="live"/> once more than <see cref="Retained"/> are held. An ended launch's totals and number stay until then: the close
    /// of its Step still takes a final sample, and a number the tick already used must not be used again or the ledger drops the sample as a duplicate.
    /// </summary>
    /// <param name="live">The launches that still run.</param>
    public void Keep(IReadOnlySet<LaunchId> live)
    {
        lock (_gate)
        {
            if (_last.Count <= Retained)
            {
                return;
            }

            foreach (var gone in _last.Keys.Where(k => !live.Contains(k)).ToList())
            {
                _ = _last.Remove(gone);
            }

            foreach (var idle in _turns.Where(t => !live.Contains(t.Key) && t.Value.CurrentCount == 1).Select(static t => t.Key).ToList())
            {
                _ = _turns.TryRemove(idle, out _);
            }
        }
    }
}
