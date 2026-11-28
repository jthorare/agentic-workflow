namespace Aw.Sindri.Features.GetWorkflowRun;

/// <summary>The tokens a run used against its budget (#3805): the run page's "N / M tokens".</summary>
/// <param name="Used">The tokens its 'prompt' and 'run_agent' node runs used, summed over their usage.</param>
/// <param name="Budget">The cap: the spec's 'budget.tokens', else the profile's 'workflow.budget.tokens'; null when neither sets one.</param>
internal sealed record RunTokens(long Used, long? Budget);
