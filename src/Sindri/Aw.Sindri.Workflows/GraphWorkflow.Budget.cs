using Aw.Sindri.Core.Graph;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The run's token total and budget (#3805): workflow state built only from activity results (the spec's budget from 'GraphLoad', each node's tokens from its outcome), so a replay rebuilds it exactly.</summary>
internal sealed partial class GraphWorkflow
{
    private long? _budget;
    private long _tokens;

    private GraphBlockOutcome? OverBudget(GraphNode node) => GraphTokenBudget.Refuse(node, _budget, _tokens);
}
