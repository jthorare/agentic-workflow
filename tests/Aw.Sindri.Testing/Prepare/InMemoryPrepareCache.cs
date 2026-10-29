using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Testing.Prepare;

/// <summary>An <see cref="IPrepareCache"/> that keeps keys, not files; every restore and store is kept.</summary>
public sealed class InMemoryPrepareCache : IPrepareCache
{
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
    private readonly List<(string Key, string Directory)> _restored = [];
    private readonly List<(string Key, string Directory)> _stored = [];

    /// <summary>The keys held.</summary>
    public IReadOnlyCollection<string> Keys => _keys;

    /// <summary>Every restore so far: the key and the package directory it went to.</summary>
    public IReadOnlyList<(string Key, string Directory)> Restored => _restored;

    /// <summary>Every store so far: the key and the package directory it came from.</summary>
    public IReadOnlyList<(string Key, string Directory)> Stored => _stored;

    /// <summary>Makes the cache hold <paramref name="key"/>.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The same instance.</returns>
    public InMemoryPrepareCache Holding(string key)
    {
        _ = _keys.Add(key);
        return this;
    }

    /// <summary>Forgets every key (what the cache holds, not what was restored or stored).</summary>
    public void Clear() => _keys.Clear();

    /// <inheritdoc/>
    public bool Has(string key) => _keys.Contains(key);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Restore(string key, string packageDirectory, CancellationToken cancellationToken)
    {
        _restored.Add((key, packageDirectory));
        return ValueTask.FromResult(Results.Success(Unit.Value));
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Store(string key, string packageDirectory, CancellationToken cancellationToken)
    {
        _ = _keys.Add(key);
        _stored.Add((key, packageDirectory));
        return ValueTask.FromResult(Results.Success(Unit.Value));
    }
}
