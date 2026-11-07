using Aw.Kernel;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The per-head fix-task files the sindri-tasks.sh hook (UserPromptSubmit) reads: one file per PR head branch, listing the open fix items.</summary>
public interface IFixTaskFiles
{
    /// <summary>Adds a fix item to the file of its PR's head branch (the file is created when missing).</summary>
    /// <param name="head">The head as "owner/repo:branch"; owner and repo are lowercased, the branch is kept as is.</param>
    /// <param name="item">The Work item's id.</param>
    /// <param name="pr">The PR number.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or the error.</returns>
    public ValueTask<Result<Unit>> Add(string head, string item, int pr, CancellationToken cancellationToken);

    /// <summary>Removes a fix item from whichever head file lists it; a file left empty is deleted.</summary>
    /// <param name="item">The Work item's id.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit (also when no file lists the item), or the error.</returns>
    public ValueTask<Result<Unit>> Remove(string item, CancellationToken cancellationToken);
}
