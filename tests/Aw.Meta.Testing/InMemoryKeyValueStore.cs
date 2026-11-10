using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IKeyValueStore: the one fake, kept honest by KeyValueStoreContract.</summary>
public sealed class InMemoryKeyValueStore : IKeyValueStore
{
    // One lock over every call: SQLite's SetIf is one statement, so two writers racing on a key (the board tests) must see the same here.
    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<Result<string?>> Get(string key, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Done.Of(_values.GetValueOrDefault(key));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Set(string key, string value, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _values[key] = value;
        }

        return Done.Unit();
    }

    /// <inheritdoc />
    public ValueTask<Result<bool>> SetIf(string key, string? expected, string value, CancellationToken cancellationToken) =>
        Done.Of(TrySetIf(key, expected, value));

    private bool TrySetIf(string key, string? expected, string value)
    {
        lock (_gate)
        {
            if (!string.Equals(_values.GetValueOrDefault(key), expected, StringComparison.Ordinal))
            {
                return false;
            }

            _values[key] = value;
            return true;
        }
    }
}
