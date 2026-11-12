namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/workflows/{slug}/runs.</summary>
/// <param name="Payload">What the manual trigger hands on, as JSON text of an object, or null for none.</param>
/// <param name="Revision">The revision the caller expects to be active; a different active revision is SND-GRAPH-007. Null runs whatever is active.</param>
internal sealed record RunWorkflowBody(string? Payload = null, int? Revision = null);
