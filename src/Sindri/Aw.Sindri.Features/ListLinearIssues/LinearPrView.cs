namespace Aw.Sindri.Features.ListLinearIssues;

/// <summary>The pull request an issue's run opened.</summary>
/// <param name="Number">The PR number.</param>
/// <param name="Url">The PR's page on the forge.</param>
/// <param name="Review">The review state in the Reviews badge's words ('waiting' when the PR waits on the owner).</param>
internal sealed record LinearPrView(int Number, string Url, string Review);
