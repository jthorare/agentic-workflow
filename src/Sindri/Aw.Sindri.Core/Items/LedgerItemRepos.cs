using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Items;

/// <summary>
/// <see cref="IItemRepos"/> over the ledger: a forward scan over the 'item.created' rows, remembered, so each lookup
/// reads only what no earlier lookup read (an item's repo never changes). It reads the ledger itself, never the views.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads an 'item.created' payload.</param>
internal sealed class LedgerItemRepos(ILedgerReader ledger, EventFactory events) : IItemRepos, IDisposable
{
    private const int Page = 500;

    private static readonly LedgerFilter _created = new(TypeFilter.Parse(EventTypes.ItemCreated).Value, Item: null);

    private readonly ConcurrentDictionary<string, RepoName?> _known = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _pass = new(1, 1);
    private long _readTo;

    public async ValueTask<Result<RepoName?>> RepoOf(ItemId item, CancellationToken cancellationToken)
    {
        var found = await Find(item, cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : found.Value.Repo;
    }

    public async ValueTask<Result<bool>> Exists(ItemId item, CancellationToken cancellationToken)
    {
        var found = await Find(item, cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : found.Value.Exists;
    }

    public void Dispose() => _pass.Dispose();

    private async ValueTask<Result<(bool Exists, RepoName? Repo)>> Find(ItemId item, CancellationToken cancellationToken)
    {
        if (_known.TryGetValue(item.Value, out var known))
        {
            return (true, known);
        }

        await _pass.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (!_known.ContainsKey(item.Value))
            {
                var scan = await ledger.Scan(_readTo, Page, _created, cancellationToken).ConfigureAwait(false);
                if (scan.IsError)
                {
                    return scan.Error;
                }

                foreach (var row in scan.Value.Events)
                {
                    Index(row);
                }

                if (scan.Value.ScannedTo <= _readTo)
                {
                    break;   // no progress: the head
                }

                _readTo = scan.Value.ScannedTo;
            }
        }
        finally
        {
            _ = _pass.Release();
        }

        return _known.TryGetValue(item.Value, out var found) ? (true, found) : (false, null);
    }

    private void Index(LedgerEvent row)
    {
        if (row.Item is null || !string.Equals(row.Envelope.Type, EventTypes.ItemCreated, StringComparison.Ordinal))
        {
            return;
        }

        var created = events.Payload<ItemCreated>(row);
        if (created.IsSuccess)
        {
            _known[row.Item] = created.Value.Repo;
        }
    }
}
