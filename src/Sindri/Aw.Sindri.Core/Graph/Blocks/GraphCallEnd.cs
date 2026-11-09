using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>How a called run ended.</summary>
/// <param name="Status">'succeeded', 'failed' or 'cancelled'.</param>
/// <param name="Outputs">The run's outputs, or null.</param>
/// <param name="Error">Why it failed, or null.</param>
public sealed record GraphCallEnd(string Status, JsonNode? Outputs, string? Error);
