using Aw.Kernel;

namespace Aw.Context.Core.ScopeGraph;

/// <summary>Where a scoping instance's graph and its agent runs' context reads are kept (issue 1005): the graph per instance, the read log per instance and run.</summary>
public interface IScopeRunStore
{
    /// <summary>Keeps the instance's graph, replacing an earlier one.</summary>
    /// <param name="instance">The workflow instance's address.</param>
    /// <param name="graph">The graph.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when stored, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<bool>> Remember(string instance, ScopeGraphView graph, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Whether the instance's graph holds the node.</summary>
    /// <param name="instance">The workflow instance's address.</param>
    /// <param name="node">The node id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True or false, null when the instance has no remembered graph, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<bool?>> Holds(string instance, string node, CancellationToken cancellationToken);

    /// <summary>Appends a read to the run's log.</summary>
    /// <param name="instance">The workflow instance's address.</param>
    /// <param name="run">The Step run's token.</param>
    /// <param name="read">The read.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when stored, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<bool>> Log(string instance, string run, GraphRead read, CancellationToken cancellationToken);

    /// <summary>The run's reads, oldest first.</summary>
    /// <param name="instance">The workflow instance's address.</param>
    /// <param name="run">The Step run's token.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The reads (none for a run that made none), or SND-CONTEXT-004.</returns>
    public ValueTask<Result<IReadOnlyList<GraphRead>>> Reads(string instance, string run, CancellationToken cancellationToken);
}
