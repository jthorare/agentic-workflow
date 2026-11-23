using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Tests.Delegate;

// A key-value store whose lane hook beats can't be read ("lane.<lane>.beat"); every other key goes to the inner store.
internal sealed class FailingBeatValues(IKeyValueStore inner) : IKeyValueStore
{
    public ValueTask<Result<string?>> Get(string key, CancellationToken cancellationToken) =>
        key.EndsWith(".beat", StringComparison.Ordinal)
            ? ValueTask.FromResult<Result<string?>>(MetaErrors.CommandFailed.With("beat store unreadable"))
            : inner.Get(key, cancellationToken);

    public ValueTask<Result<Unit>> Set(string key, string value, CancellationToken cancellationToken) => inner.Set(key, value, cancellationToken);

    public ValueTask<Result<bool>> SetIf(string key, string? expected, string value, CancellationToken cancellationToken) => inner.SetIf(key, expected, value, cancellationToken);
}
