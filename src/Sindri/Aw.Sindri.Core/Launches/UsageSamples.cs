using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launches;

/// <summary>
/// The last totals sampled per launch and the last sample number used, in memory. A restart samples once more, which is harmless:
/// the number is never lower than the clock's and never repeats within a run, so no sample is dropped as a duplicate key.
/// </summary>
public sealed class UsageSamples
{
    private readonly Lock _gate = new();
    private readonly Dictionary<LaunchId, (long Input, long Output, int Number)> _last = [];

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

    /// <summary>Forgets every launch not in <paramref name="live"/>.</summary>
    /// <param name="live">The launches that still run.</param>
    public void Keep(IReadOnlySet<LaunchId> live)
    {
        lock (_gate)
        {
            foreach (var gone in _last.Keys.Where(k => !live.Contains(k)).ToList())
            {
                _ = _last.Remove(gone);
            }
        }
    }
}
