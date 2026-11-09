namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>Where Sindri draws one node. Read so the contract holds; part 1 of the Workflows page draws no graph.</summary>
/// <param name="Node">The node id.</param>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
internal sealed record NodePositionView(string Node, double X, double Y);
