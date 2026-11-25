namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>POST /sindri/workflows/{slug}/test: a test run was started.</summary>
/// <param name="Run">The run's id.</param>
/// <param name="Slug">The workflow.</param>
/// <param name="Revision">The revision it walks.</param>
/// <param name="Mode">Always test.</param>
internal sealed record WorkflowTestStarted(string Run, string Slug, int Revision, string Mode);
