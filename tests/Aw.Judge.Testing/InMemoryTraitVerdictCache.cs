using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Testing;

/// <summary>
/// ITraitVerdictCache in memory, with the real one's rules: a read skips verdicts older than <paramref name="keep"/>, a
/// write keeps only the newest <paramref name="maxEntries"/>. When <see cref="Unavailable"/> it finds nothing and keeps
/// nothing, like a cache whose file can't be opened.
/// </summary>
/// <param name="clock">When a verdict was stored.</param>
/// <param name="keep">How long a verdict is used.</param>
/// <param name="maxEntries">The most verdicts kept.</param>
public sealed class InMemoryTraitVerdictCache(TimeProvider clock, TimeSpan keep, int maxEntries) : ITraitVerdictCache
{
    private readonly Dictionary<string, (ChoiceBatchAnswer Answer, DateTimeOffset At, long Order)> _verdicts = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private long _order;

    /// <summary>An empty cache on the real clock with the real defaults (7 days, 100,000 verdicts).</summary>
    public InMemoryTraitVerdictCache()
        : this(TimeProvider.System, TimeSpan.FromDays(7), 100_000)
    {
    }

    /// <summary>Finds nothing and keeps nothing.</summary>
    public bool Unavailable { get; init; }

    /// <summary>How many lookups asked for at least one key.</summary>
    public int Lookups { get; private set; }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, ChoiceBatchAnswer>> Get(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Dictionary<string, ChoiceBatchAnswer> found = new(StringComparer.Ordinal);
        lock (_gate)
        {
            Lookups += keys.Count > 0 ? 1 : 0;
            var cutoff = clock.GetUtcNow() - keep;
            foreach (var key in keys)
            {
                if (!Unavailable && _verdicts.TryGetValue(key, out var verdict) && verdict.At >= cutoff)
                {
                    found[key] = verdict.Answer;
                }
            }
        }

        return ValueTask.FromResult<IReadOnlyDictionary<string, ChoiceBatchAnswer>>(found);
    }

    /// <inheritdoc />
    public ValueTask Put(IReadOnlyDictionary<string, ChoiceBatchAnswer> verdicts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verdicts);
        lock (_gate)
        {
            if (Unavailable || verdicts.Count == 0)
            {
                return ValueTask.CompletedTask;
            }

            var now = clock.GetUtcNow();
            foreach (var (key, answer) in verdicts)
            {
                _verdicts[key] = (answer, now, _order++);
            }

            foreach (var key in _verdicts.Where(entry => entry.Value.At < now - keep).Select(entry => entry.Key).ToList())
            {
                _ = _verdicts.Remove(key);
            }

            foreach (var key in _verdicts.OrderByDescending(entry => entry.Value.At).ThenByDescending(entry => entry.Value.Order).Skip(maxEntries).Select(entry => entry.Key).ToList())
            {
                _ = _verdicts.Remove(key);
            }
        }

        return ValueTask.CompletedTask;
    }
}
