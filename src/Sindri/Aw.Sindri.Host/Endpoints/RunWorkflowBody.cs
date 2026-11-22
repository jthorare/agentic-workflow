namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/workflows/{slug}/runs.</summary>
/// <param name="Payload">What the manual trigger hands on, as JSON text of an object, or null for none.</param>
/// <param name="Revision">A saved revision to run once instead of the active one, or null for the active one (#3691); the active revision does not change.</param>
internal sealed record RunWorkflowBody(string? Payload = null, int? Revision = null);
