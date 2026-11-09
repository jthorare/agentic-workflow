namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/workflows/{slug}/activate.</summary>
/// <param name="Revision">The revision to run, or null for the newest.</param>
internal sealed record ActivateWorkflowBody(int? Revision = null);
