using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The fix-task files under '$AW_SINDRI_HOME/fix-tasks/' (one per PR head), which the sindri-tasks hook reads to tell a session about the fixes asked of it.</summary>
public interface IFixTaskFiles
{
    /// <summary>Adds the task for <paramref name="item"/> on PR <paramref name="pr"/> to the file of <paramref name="head"/> ('owner/repo:branch'), once.</summary>
    /// <param name="head">The PR head, 'owner/repo:branch'.</param>
    /// <param name="item">The fix item.</param>
    /// <param name="pr">The PR number.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when the task was added, false when it was already there.</returns>
    public ValueTask<Result<bool>> Add(string head, ItemId item, int pr, CancellationToken cancellationToken);

    /// <summary>Removes the task for <paramref name="item"/> from whichever head file holds it; the file goes when its list empties.</summary>
    /// <param name="item">The fix item.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when a task was removed, false when none held the item.</returns>
    public ValueTask<Result<bool>> Remove(ItemId item, CancellationToken cancellationToken);
}
