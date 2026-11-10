using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IKeyValueStore: the one fake, kept honest by KeyValueStoreContract.</summary>
public sealed class InMemoryKeyValueStore : IKeyValueStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<Result<string?>> Get(string key, CancellationToken cancellationToken) =>
        Done.Of(_values.GetValueOrDefault(key));

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Set(string key, string value, CancellationToken cancellationToken)
    {
        _values[key] = value;
        return Done.Unit();
    }

    /// <inheritdoc />
    public ValueTask<Result<bool>> SetIf(string key, string? expected, string value, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
