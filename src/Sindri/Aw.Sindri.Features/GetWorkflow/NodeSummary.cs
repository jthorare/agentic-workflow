namespace Aw.Sindri.Features.GetWorkflow;

/// <summary>The line the canvas shows on one node.</summary>
/// <param name="Node">The node's id.</param>
/// <param name="Summary">The line, always set.</param>
/// <param name="SummaryProblem">What is not set yet, or null.</param>
internal sealed record NodeSummary(string Node, string Summary, string? SummaryProblem);
