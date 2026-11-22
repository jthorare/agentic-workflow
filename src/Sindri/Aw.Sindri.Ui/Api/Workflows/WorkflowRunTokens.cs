namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>GET /sindri/workflow-runs/{run}'s 'tokens' (#3805): what the run's prompt and run_agent nodes used, and its budget.</summary>
/// <param name="Used">The tokens used so far.</param>
/// <param name="Budget">The run's cap, or null when it has none.</param>
internal sealed record WorkflowRunTokens(long Used, long? Budget);
