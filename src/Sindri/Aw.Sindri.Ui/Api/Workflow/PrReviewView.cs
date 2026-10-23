namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The review threads of a pull request, counted.</summary>
/// <param name="Open">Threads still open.</param>
/// <param name="Resolved">Threads resolved.</param>
internal sealed record PrReviewView(int Open, int Resolved);
