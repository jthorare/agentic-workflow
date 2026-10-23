namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>Where the pull request's documentation stands.</summary>
/// <param name="Status">"updated", "notNeeded" or "missing", as served.</param>
internal sealed record PrDocsView(string Status);
