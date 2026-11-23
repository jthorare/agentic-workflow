namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>The body of POST /sindri/workflows/{slug}/runs. The field is always written (Sindri binds required constructor parameters), as null when the run hands on nothing.</summary>
/// <param name="Payload">What the manual trigger hands on, as JSON text of an object, or null for none.</param>
internal sealed record RunWorkflowBody(string? Payload);
