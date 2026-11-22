using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What a succeeded run gives as its result (#3807): the output of the node its spec's 'result' names, or of the nodes no edge leaves.</summary>
internal static class GraphResultOutputs
{
    /// <summary>The run's outputs as JSON text.</summary>
    /// <param name="spec">The spec the run walked.</param>
    /// <param name="rows">The run's node rows.</param>
    /// <returns>The text, or null when no result node succeeded.</returns>
    internal static string? Build(GraphSpec spec, IReadOnlyList<NodeRun> rows) => throw new NotSupportedException();
}
