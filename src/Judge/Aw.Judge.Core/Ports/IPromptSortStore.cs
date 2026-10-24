using Aw.Judge.Core.Ids;
using Aw.Judge.Core.PromptSort;
using Aw.Kernel;

namespace Aw.Judge.Core.Ports;

/// <summary>Where the prompt sorter keeps its axes and runs (the 'prompt_sort_axes' and 'prompt_sort_runs' tables, migration 0003). Every call returns an error, never throws.</summary>
public interface IPromptSortStore
{
    /// <summary>Saves a prompt's axes and run. Saving a decision's record again replaces it.</summary>
    /// <param name="record">The record; its decision is already in the decision log.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why the store can't be written.</returns>
    public ValueTask<Result<Unit>> Save(PromptSortRecord record, CancellationToken cancellationToken);

    /// <summary>Finds what the sorter kept for a decision.</summary>
    /// <param name="decision">The prompt-sort decision.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The record, or AW-JUDGE-002 when the sorter kept nothing for it.</returns>
    public ValueTask<Result<PromptSortRecord>> Find(DecisionId decision, CancellationToken cancellationToken);
}
