namespace Aw.Sindri.Features.SuggestLinks;

/// <summary>One suggested link: the issue and the pull request that names it.</summary>
/// <param name="Key">The issue's human key.</param>
/// <param name="Repo">The pull request's repo, 'owner/name'.</param>
/// <param name="Number">The pull request's number.</param>
internal sealed record LinkSuggestion(string Key, string Repo, int Number);
