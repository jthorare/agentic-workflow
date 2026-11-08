using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Testing.Memory;

/// <summary>IMemoryStore in memory. Held to <see cref="MemoryStoreContract"/>.</summary>
public sealed class InMemoryMemoryStore : IMemoryStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, MemoryInfo> _memories = new(StringComparer.Ordinal);
    private readonly List<MemoryFact> _facts = [];

    /// <summary>Gets every stored fact, oldest first (for assertions).</summary>
    public IReadOnlyList<MemoryFact> AllFacts
    {
        get
        {
            lock (_gate)
            {
                return [.. _facts];
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Create(MemoryInfo memory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(memory);
        lock (_gate)
        {
            return ValueTask.FromResult<Result<Unit>>(_memories.TryAdd(memory.Name.Value, memory)
                ? Unit.Value
                : SindriErrors.MemoryNameTaken.With($"A memory named {memory.Name} exists."));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<MemoryInfo>> Get(MemoryName name, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<MemoryInfo>>(_memories.TryGetValue(name.Value, out var found) ? found : NotFound(name));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<MemoryInfo>>> List(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<MemoryInfo>>>(_memories.Values.OrderBy(static m => m.Name.Value, StringComparer.Ordinal).ToList());
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyDictionary<string, int>>> Counts(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<IReadOnlyDictionary<string, int>>>(_facts.GroupBy(static f => f.Store.Value, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.Count(), StringComparer.Ordinal));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<MemoryFact>>> Facts(MemoryName name, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<MemoryFact>>>(_memories.ContainsKey(name.Value)
                ? _facts.Where(f => f.Store == name).OrderBy(static f => f.CreatedAt).ToList()
                : NotFound(name));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Add(MemoryFact fact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fact);
        lock (_gate)
        {
            if (!_memories.ContainsKey(fact.Store.Value))
            {
                return ValueTask.FromResult<Result<Unit>>(NotFound(fact.Store));
            }

            _facts.Add(fact);
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<bool>> Refresh(MemoryName memory, string key, DateTimeOffset refreshedAt, DateTimeOffset? expiresAt, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var index = _facts.FindIndex(f => f.Store == memory && string.Equals(f.Key, key, StringComparison.Ordinal));
            if (index < 0)
            {
                return ValueTask.FromResult<Result<bool>>(false);
            }

            _facts[index] = _facts[index] with { RefreshedAt = refreshedAt, ExpiresAt = expiresAt ?? _facts[index].ExpiresAt };
            return ValueTask.FromResult<Result<bool>>(true);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<int>> Delete(MemoryName memory, FactSelector selector, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selector);
        lock (_gate)
        {
            if (!_memories.ContainsKey(memory.Value))
            {
                return ValueTask.FromResult<Result<int>>(NotFound(memory));
            }

            return ValueTask.FromResult<Result<int>>(_facts.RemoveAll(f => f.Store == memory && Matches(f, selector)));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<int>> DeleteExpired(DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<int>>(_facts.RemoveAll(f => f.ExpiresAt is { } at && at <= now));
        }
    }

    private static bool Matches(MemoryFact fact, FactSelector selector)
        => (selector.Key is not null && string.Equals(fact.Key, selector.Key, StringComparison.Ordinal))
            || (selector.Tag is not null && fact.Tags.Contains(selector.Tag, StringComparer.Ordinal))
            || (selector.ExpiredAt is { } now && fact.ExpiresAt is { } at && at <= now);

    private static Error NotFound(MemoryName name) => SindriErrors.MemoryNotFound.With($"No memory named {name}.");
}
