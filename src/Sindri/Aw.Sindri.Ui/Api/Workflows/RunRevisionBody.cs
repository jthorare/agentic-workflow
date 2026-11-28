namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>The body of POST /sindri/workflows/{slug}/runs for "Run revision N once" (#3691). Run now keeps <see cref="RunWorkflowBody"/>, so its body stays as it was.</summary>
/// <param name="Payload">What the manual trigger hands on, as JSON text of an object, or null for none.</param>
/// <param name="Revision">The saved revision to run once; the live revision does not change.</param>
internal sealed record RunRevisionBody(string? Payload, int Revision);
