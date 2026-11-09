using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Items;

/// <summary>Makes a work item from inside another command that already holds the command gate (a handoff), as the create-item command does.</summary>
public interface IItemMaker
{
    /// <summary>Appends 'item.created'.</summary>
    /// <param name="title">The title: 1 to 200 characters.</param>
    /// <param name="repo">The profile repo the item belongs to, or null for the caller's own.</param>
    /// <param name="body">The body, or null: at most 4,000 characters.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The new item's id, or the error.</returns>
    public ValueTask<Result<ItemId>> Create(string title, RepoName? repo, string? body, CancellationToken cancellationToken);
}
