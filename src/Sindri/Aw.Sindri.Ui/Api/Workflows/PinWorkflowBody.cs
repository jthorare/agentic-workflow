using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>The body of POST /sindri/workflows/{slug}/pins: the step, and the output or a run to copy it from.</summary>
/// <param name="Node">The step's node id.</param>
/// <param name="Run">A run id to copy the step's output from, or null.</param>
/// <param name="Output">The output as JSON, or null when a run is given.</param>
/// <param name="Revision">The revision the pin is taken on, or null for the newest.</param>
internal sealed record PinWorkflowBody(string Node, string? Run, JsonElement? Output, int? Revision);
