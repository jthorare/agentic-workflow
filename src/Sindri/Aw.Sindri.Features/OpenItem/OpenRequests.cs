using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.OpenItem;

/// <summary>
/// The open requests this process has appended, for the one check the views can't answer yet: the 'launches' view is folded after the
/// append, so a second open that arrives first would see no request in flight. Opens are taken one at a time (a person's clicks, not a
/// stream), and a request counts as in flight here until the view shows it or its deadline passes.
/// </summary>
internal sealed class OpenRequests(TimeProvider clock) : IDisposable
{
    private readonly SemaphoreSlim _turn = new(1, 1);
    private readonly Lock _gate = new();
    private readonly Dictionary<ItemId, (LaunchId Launch, DateTimeOffset At)> _recent = [];

    /// <summary>Waits for this process's turn to decide an open; dispose the result to give it up.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The turn.</returns>
    public async ValueTask<IDisposable> Turn(CancellationToken cancellationToken)
    {
        await _turn.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Release(_turn);
    }

    /// <summary>Notes that a request was appended for the item.</summary>
    /// <param name="item">The item.</param>
    /// <param name="launch">The request's launch.</param>
    public void Note(ItemId item, LaunchId launch)
    {
        lock (_gate)
        {
            _recent[item] = (launch, clock.GetUtcNow());
        }
    }

    /// <summary>The launch noted for the item that the view hasn't caught up with and that is still inside its deadline, or null.</summary>
    /// <param name="item">The item.</param>
    /// <param name="seen">The launches the view already shows for the item.</param>
    /// <returns>The launch still in flight, or null.</returns>
    public LaunchId? Unseen(ItemId item, IReadOnlyList<LaunchRow> seen)
    {
        ArgumentNullException.ThrowIfNull(seen);
        lock (_gate)
        {
            if (!_recent.TryGetValue(item, out var noted))
            {
                return null;
            }

            if (seen.Any(row => row.Id == noted.Launch) || clock.GetUtcNow() - noted.At >= LaunchRules.Deadline)
            {
                _ = _recent.Remove(item);   // the view has it (and judges it itself), or it is past its deadline
                return null;
            }

            return noted.Launch;
        }
    }

    public void Dispose() => _turn.Dispose();

    private sealed class Release(SemaphoreSlim turn) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _ = turn.Release();
            }
        }
    }
}
