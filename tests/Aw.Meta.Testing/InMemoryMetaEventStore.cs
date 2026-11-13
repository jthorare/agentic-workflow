using Aw.Kernel;
using Aw.Meta.Core.Events;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IMetaEventStore: the one fake, kept honest by MetaEventStoreContract.</summary>
public sealed class InMemoryMetaEventStore : IMetaEventStore
{
    private readonly List<MetaEvent> _events = [];
    private long _last;

    /// <inheritdoc />
    public ValueTask<Result<long>> Append(MetaEvent metaEvent, CancellationToken cancellationToken)
    {
        var checkedEvent = MetaEventRules.Check(metaEvent);
        if (checkedEvent.IsError)
        {
            return Done.Failed<long>(checkedEvent.Error);
        }

        _ = _events.RemoveAll(e => e.At < checkedEvent.Value.At - MetaEvent.Retention);
        _last++;
        _events.Add(checkedEvent.Value with { Id = _last });
        return Done.Of(_last);
    }

    /// <inheritdoc />
    public ValueTask<Result<MetaEventPage>> Read(long after, string? kind, string? lane, int max, CancellationToken cancellationToken) =>
        Done.Of(new MetaEventPage(
            [.. _events
                .Where(e => e.Id > after && (kind is null || e.Kind == kind) && (lane is null || e.Lane == lane))
                .OrderBy(static e => e.Id)
                .Take(max)],
            _events.Count == 0 ? _last + 1 : _events.Min(static e => e.Id),
            _last));

    /// <inheritdoc />
    public ValueTask<Result<int>> Prune(DateTimeOffset olderThan, CancellationToken cancellationToken) =>
        Done.Of(_events.RemoveAll(e => e.At < olderThan));
}
