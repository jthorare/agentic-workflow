namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>The body of POST /sindri/workflows/{slug}/activate. The field is always written (Sindri binds required constructor parameters), as null for the newest revision.</summary>
/// <param name="Revision">The revision to run, or null for the newest.</param>
internal sealed record ActivateWorkflowBody(int? Revision);
