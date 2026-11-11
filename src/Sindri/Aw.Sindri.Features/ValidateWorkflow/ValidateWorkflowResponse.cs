namespace Aw.Sindri.Features.ValidateWorkflow;

/// <summary>What the activation checks found.</summary>
/// <param name="Passed">True when no finding is an error, so the workflow can be activated.</param>
/// <param name="Hash">The graph's hash when it passed, else null.</param>
/// <param name="Findings">The findings, errors and warnings.</param>
internal sealed record ValidateWorkflowResponse(bool Passed, string? Hash, IReadOnlyList<WorkflowFinding> Findings);
