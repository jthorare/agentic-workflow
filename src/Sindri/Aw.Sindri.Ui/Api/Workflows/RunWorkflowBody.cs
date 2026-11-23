namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>The body of POST /sindri/workflows/{slug}/runs. The fields are always written (Sindri binds required constructor parameters), as null when unused.</summary>
/// <param name="Payload">What the manual trigger hands on, as JSON text of an object, or null for none.</param>
/// <param name="Revision">A saved revision to run once instead of the live one (#3691), or null for the live one.</param>
internal sealed record RunWorkflowBody(string? Payload, int? Revision = null);
