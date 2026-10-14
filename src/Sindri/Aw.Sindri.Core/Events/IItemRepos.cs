using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The repo each item belongs to, for the session repo filter (API spec §3.1, §5.2): read from the item's item.created
/// event in the ledger, never the views, so an item created a moment ago isn't missed while the projector catches up.
/// </summary>
public interface IItemRepos
{
    /// <summary>The repo <paramref name="item"/> was created in, or null when it has none or isn't in the ledger.</summary>
    /// <param name="item">The item.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The repo, or null.</returns>
    public ValueTask<Result<RepoName?>> RepoOf(ItemId item, CancellationToken cancellationToken);
}
