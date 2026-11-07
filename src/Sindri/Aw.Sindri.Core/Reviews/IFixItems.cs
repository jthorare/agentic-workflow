using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Reviews;

/// <summary>Makes the Work item of a fix task (PR review page spec §8.2 step 1), as the create-item command does but inside the fix command, which already holds the command gate.</summary>
public interface IFixItems
{
    /// <summary>Appends 'item.created' for the fix task.</summary>
    /// <param name="title">The title: 1 to 200 characters.</param>
    /// <param name="repo">The profile repo the item belongs to, or null for the caller's own.</param>
    /// <param name="body">The body: at most 4,000 characters.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The new item's id, or the error.</returns>
    public ValueTask<Result<ItemId>> Create(string title, RepoName? repo, string body, CancellationToken cancellationToken);
}
