namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/workflows/{slug}/runs.</summary>
/// <param name="Payload">What the manual trigger hands on, as JSON text of an object, or null for none.</param>
/// <param name="Source">'cli' when 'sindri wf run' sends it (the run list then says "cli"); the owner's calls only, a session keeps its own name. Null otherwise (#3683).</param>
internal sealed record RunWorkflowBody(string? Payload = null, string? Source = null);
