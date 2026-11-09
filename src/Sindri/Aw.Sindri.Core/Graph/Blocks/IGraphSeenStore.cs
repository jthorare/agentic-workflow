using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What an 'only_new' node remembers: the keys of the items a workflow has already passed on ('workflow_seen' in ledger.db).</summary>
public interface IGraphSeenStore
{
    /// <summary>Finds which of <paramref name="keys"/> the workflow has seen.</summary>
    /// <param name="workflow">The workflow.</param>
    /// <param name="keys">The item keys to look up.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The keys that were seen; none when the store has never been written.</returns>
    public ValueTask<Result<IReadOnlySet<string>>> Seen(WorkflowId workflow, IReadOnlyList<string> keys, CancellationToken cancellationToken);

    /// <summary>Records <paramref name="keys"/> as seen; a key already there keeps its first time.</summary>
    /// <param name="workflow">The workflow.</param>
    /// <param name="keys">The item keys.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why the keys could not be written.</returns>
    public ValueTask<Result<Unit>> Record(WorkflowId workflow, IReadOnlyList<string> keys, CancellationToken cancellationToken);
}
