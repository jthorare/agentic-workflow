namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>The line GET /sindri/workflows/{slug} serves for one node.</summary>
/// <param name="Node">The node's id.</param>
/// <param name="Summary">The line, in plain words.</param>
/// <param name="SummaryProblem">What is not set yet, or null.</param>
/// <param name="KindTitle">The block's catalog name, such as "Code"; null for a type the catalog doesn't know or a server that predates the field.</param>
internal sealed record NodeSummaryView(string Node, string Summary, string? SummaryProblem, string? KindTitle = null);
