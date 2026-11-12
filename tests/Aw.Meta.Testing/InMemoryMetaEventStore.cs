using Aw.Kernel;
using Aw.Meta.Core.Events;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IMetaEventStore: the one fake, kept honest by MetaEventStoreContract.</summary>
public sealed class InMemoryMetaEventStore : IMetaEventStore
{
    /// <inheritdoc />
    public ValueTask<Result<long>> Append(MetaEvent metaEvent, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<MetaEvent>>> Read(long after, string? kind, string? lane, int max, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<int>> Prune(DateTimeOffset olderThan, CancellationToken cancellationToken) => throw new NotSupportedException();
}
