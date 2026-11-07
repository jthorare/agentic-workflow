namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>The pull request linked to an issue.</summary>
/// <param name="Number">The PR number.</param>
/// <param name="Url">The PR's page.</param>
/// <param name="Review">The review state, in the Reviews badge's words.</param>
internal sealed record LinearPrRef(int Number, string Url, string Review);
