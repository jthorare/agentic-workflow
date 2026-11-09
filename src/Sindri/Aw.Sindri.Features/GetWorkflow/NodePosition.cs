using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.GetWorkflow;

/// <summary>Where Sindri draws one node on the canvas (agents never send layout).</summary>
/// <param name="Node">The node.</param>
/// <param name="X">Left edge, from the canvas origin.</param>
/// <param name="Y">Top edge, from the canvas origin.</param>
internal sealed record NodePosition(NodeId Node, double X, double Y);
