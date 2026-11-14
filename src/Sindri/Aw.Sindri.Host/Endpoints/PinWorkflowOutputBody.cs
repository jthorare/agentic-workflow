using System.Text.Json.Nodes;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/workflows/{slug}/pins: the step, and either the output or a run to copy it from.</summary>
/// <param name="Node">The step's node id.</param>
/// <param name="Run">A run id to copy the step's output from, or null.</param>
/// <param name="Output">The output as JSON, or null when a run is given.</param>
/// <param name="Revision">The revision the pin is taken on, or null for the newest.</param>
internal sealed record PinWorkflowOutputBody(string? Node = null, string? Run = null, JsonNode? Output = null, int? Revision = null);
