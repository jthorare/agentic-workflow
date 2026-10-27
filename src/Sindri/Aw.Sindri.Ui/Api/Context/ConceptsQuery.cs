namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What GET /sindri/context/concepts is asked: a repo and a page.</summary>
/// <param name="Repo">The repo to read; empty for the profile's only repo.</param>
/// <param name="Cursor">The previous page's next; null for the first page.</param>
/// <param name="Limit">The page size; null leaves it to the server.</param>
/// <param name="Q">Words to narrow the concepts by; null for all.</param>
internal sealed record ConceptsQuery(string Repo, string? Cursor = null, int? Limit = null, string? Q = null);
