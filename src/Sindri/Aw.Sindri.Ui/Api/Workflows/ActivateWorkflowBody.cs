namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>The body of POST /sindri/workflows/{slug}/activate. The field is always written (Sindri binds required constructor parameters), as null for the newest revision.</summary>
/// <param name="Revision">The revision to run, or null for the newest.</param>
/// <param name="IfLatest">True to refuse when a newer revision exists.</param>
/// <param name="UntestedOk">True to activate a revision with no passing test run (the "Activate anyway" confirm).</param>
internal sealed record ActivateWorkflowBody(int? Revision, bool IfLatest = false, bool UntestedOk = false);
