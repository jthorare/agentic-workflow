using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launches;

/// <summary>The last totals sampled per launch, in memory: a restart samples once more, which is harmless (the sample number comes from the clock, not from here).</summary>
public sealed class UsageSamples
{
    private readonly Lock _gate = new();
    private readonly Dictionary<LaunchId, (long Input, long Output)> _last = [];

    /// <summary>Whether <paramref name="input"/> and <paramref name="output"/> differ from the last totals noted for the launch; notes them when they do.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="input">Input tokens so far.</param>
    /// <param name="output">Output tokens so far.</param>
    /// <returns>True when the totals are new.</returns>
    public bool Changed(LaunchId launch, long input, long output)
    {
        lock (_gate)
        {
            if (_last.TryGetValue(launch, out var seen) && seen == (input, output))
            {
                return false;
            }

            _last[launch] = (input, output);
            return true;
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
