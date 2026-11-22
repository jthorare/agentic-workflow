using System.Globalization;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Loops;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// A run's token budget (#3805): a 'prompt' or 'run_agent' node that would start once the run's total has reached the budget fails with SND-GRAPH-134 and never runs.
/// A pure comparison, so the walk adds no command for it: a run with no budget, or under it, keeps the exact command sequence it had before.
/// </summary>
internal static class GraphTokenBudget
{
    /// <summary>The failed outcome of a node the budget refuses.</summary>
    /// <param name="node">The node about to start.</param>
    /// <param name="budget">The run's budget, or null for no cap.</param>
    /// <param name="used">The tokens the run has used so far.</param>
    /// <returns>The outcome, or null when the node may start.</returns>
    internal static GraphBlockOutcome? Refuse(GraphNode node, long? budget, long used) =>
        budget is { } cap && used >= cap && Counts(node.Type)
            ? new GraphBlockOutcome(NodeRunStatus.Failed, Input: null, Output: null, [], Text(node, cap, used))
            : null;

    private static bool Counts(string type) =>
        string.Equals(type, "prompt", StringComparison.Ordinal) || string.Equals(type, LoopBody.RunAgentType, StringComparison.Ordinal);

    private static string Text(GraphNode node, long cap, long used)
    {
        var error = SindriErrors.GraphOverBudget.With(string.Create(CultureInfo.InvariantCulture, $"the run has used {used} of its {cap}-token budget, so '{node.Id.Value}' did not start"));
        return $"{error.Code}: {error.Message}";
    }
}
