using Aw.Sindri.Core.Graph;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The batch's share of the run's token budget (#3805): the budget and the run's total come in the batch's input, the batch adds what its nodes use and returns its own
/// part, so the parent's total stays right across batches. The items of a batch run one after the other, so each node is checked against every item before it.
/// </summary>
internal sealed partial class GraphIterationWorkflow
{
    private long? _budget;
    private long _used;
    private long _spent;

    private GraphBlockOutcome? OverBudget(GraphNode node) => GraphTokenBudget.Refuse(node, _budget, _used + _spent);
}
