using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Desk;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The 'GetInstance' view of the instances a list shows, remembering the ones that ended: an ended instance's view never changes, and
/// reading it replays the whole workflow, so a list that includes every ended instance reads each of them once, not on every call.
/// A view is remembered by its address and start time (a new run on the same address starts later and is read afresh), only once it has an outcome.
/// </summary>
/// <param name="reader">The 'GetInstance' Query.</param>
public sealed class EndedInstanceViews(IInstanceReader reader)
{
    private readonly ConcurrentDictionary<(string Address, DateTimeOffset Started), InstanceView> _ended = new();
    private readonly Lock _gate = new();
    private (string Address, DateTimeOffset Started)? _newest;

    /// <summary>The most ended views kept; past it the newest one is dropped to make room (a list longer than this keeps re-reading only the last few rows, where dropping the oldest would re-read all of them).</summary>
    public const int Capacity = 4096;

    /// <summary>The view of <paramref name="address"/>, always read: this is the lookup of one instance, not a list row.</summary>
    /// <param name="address">The instance.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The view, or the reader's error.</returns>
    public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) => reader.Get(address, cancellationToken);

    /// <summary>The view of a listed row: remembered when the instance had ended, else read.</summary>
    /// <param name="row">The listed row (its start time keys the memory).</param>
    /// <param name="address">The row's instance address.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The view, or the reader's error.</returns>
    public async ValueTask<Result<InstanceView>> Get(InstanceRow row, InstanceAddress address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.StartedAt is not { } started)
        {
            return await reader.Get(address, cancellationToken).ConfigureAwait(false);   // no start time to tell this run from a later one on the address: always read
        }

        if (_ended.TryGetValue((address.Value, started), out var remembered))
        {
            return remembered;
        }

        var view = await reader.Get(address, cancellationToken).ConfigureAwait(false);
        if (view.IsSuccess && view.Value.Outcome is not null)
        {
            Remember((address.Value, started), view.Value);
        }

        return view;
    }

    private void Remember((string Address, DateTimeOffset Started) key, InstanceView view)
    {
        lock (_gate)
        {
            if (_ended.Count >= Capacity && !_ended.ContainsKey(key) && _newest is { } newest)
            {
                _ = _ended.TryRemove(newest, out _);
            }

            _ended[key] = view;
            _newest = key;
        }
    }
}
