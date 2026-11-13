namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/workflows/{slug}/activate.</summary>
/// <param name="Revision">The revision to run, or null for the newest.</param>
/// <param name="IfLatest">True to refuse when a newer revision exists.</param>
/// <param name="UntestedOk">True to activate a revision with no passing test run.</param>
internal sealed record ActivateWorkflowBody(int? Revision = null, bool IfLatest = false, bool UntestedOk = false);
