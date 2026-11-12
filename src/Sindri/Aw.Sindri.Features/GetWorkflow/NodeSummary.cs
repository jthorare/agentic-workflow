namespace Aw.Sindri.Features.GetWorkflow;

/// <summary>The line the canvas shows on one node.</summary>
/// <param name="Node">The node's id.</param>
/// <param name="Summary">The line, always set.</param>
/// <param name="SummaryProblem">What is not set yet, or null.</param>
/// <param name="KindTitle">The block's catalog Title, the one name for this kind of step (Workflows page spec decision 6, E5); null only for a block type the catalog doesn't know.</param>
internal sealed record NodeSummary(string Node, string Summary, string? SummaryProblem, string? KindTitle = null);
