namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The proof run of a pull request.</summary>
/// <param name="Status">"passed", "failed" or "none", as served.</param>
internal sealed record PrProofView(string Status);
