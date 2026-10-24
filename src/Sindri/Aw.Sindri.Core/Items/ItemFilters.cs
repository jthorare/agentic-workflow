using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Items;

/// <summary>
/// The item an event read names (API spec §3.1, §5.2): an id or tracker alias, resolved for the caller once per read. An id resolves
/// through the ledger (<see cref="IItemRepos"/>), so an item created a moment ago is found before the projector has folded it
/// (scoping never depends on view freshness); only an alias needs the views.
/// </summary>
public static class ItemFilters
{
    /// <summary>
    /// The id <paramref name="reference"/> names, or null when the read has no item filter; SND-ITEM-404 when there is no such item, SND-CLI-003 when a filter comes without a caller.
    /// </summary>
    /// <param name="reference">The reference, or null.</param>
    /// <param name="items">Resolves an alias.</param>
    /// <param name="repos">Resolves an id, from the ledger.</param>
    /// <param name="callers">The caller, whose repo scope applies.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The id, or null.</returns>
    public static async ValueTask<Result<ItemId?>> Resolve(ItemRef? reference, IItemResolver items, IItemRepos repos, ICallerContextAccessor callers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(callers);
        if (reference is not { } named)
        {
            return (ItemId?)null;
        }

        if (callers.Current is not { } caller)
        {
            return SindriErrors.InvalidInput.With("an item filter needs a caller");
        }

        if (named.Id is not { } id)
        {
            var row = await items.Resolve(named, caller, cancellationToken).ConfigureAwait(false);
            return row.IsError ? row.Error : row.Value.Id;
        }

        return await ById(id, repos, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Result<ItemId?>> ById(ItemId id, IItemRepos repos, CancellationToken cancellationToken)
    {
        var exists = await repos.Exists(id, cancellationToken).ConfigureAwait(false);
        if (exists.IsError)
        {
            return exists.Error;
        }

        return exists.Value ? id : SindriErrors.ItemNotFound.With($"no item '{id.Value}'");
    }
}
