namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>The line GET /sindri/workflows/{slug} serves for one node.</summary>
/// <param name="Node">The node's id.</param>
/// <param name="Summary">The line, in plain words.</param>
/// <param name="SummaryProblem">What is not set yet, or null.</param>
internal sealed record NodeSummaryView(string Node, string Summary, string? SummaryProblem);
