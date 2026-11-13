namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>POST /sindri/workflows/validate: what the activation checks found.</summary>
/// <param name="Passed">True when no finding is an error.</param>
/// <param name="Hash">The graph's hash when it passed, else null.</param>
/// <param name="Findings">The findings, errors and warnings.</param>
internal sealed record WorkflowValidated(bool Passed, string? Hash, IReadOnlyList<WorkflowFindingView> Findings);
