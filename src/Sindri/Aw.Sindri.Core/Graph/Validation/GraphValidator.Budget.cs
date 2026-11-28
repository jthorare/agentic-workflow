namespace Aw.Sindri.Core.Graph.Validation;

public sealed partial class GraphValidator
{
    // #3805: a cap of 0 or less would refuse the run's first prompt, and a 'budget' with no 'tokens' caps nothing, so both fail on save and not on the first run
    private static void Budget(GraphSpec spec, List<GraphFinding> found)
    {
        if (spec.Budget is { } budget && budget.Tokens is not > 0)
        {
            found.Add(new GraphFinding("GRAPH-BUDGET-001", GraphSeverity.Error, [], "budget.tokens", "budget.tokens must be a whole number above 0: the most tokens one run's prompt and run_agent steps may use", "set budget.tokens to a number such as 200000, or remove budget"));
        }
    }
}
