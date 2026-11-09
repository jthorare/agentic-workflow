namespace Aw.Sindri.Features.SearchWorkflowDocs;

/// <summary>One page in a ranking or the page list.</summary>
/// <param name="Slug">The page's slug, which 'page' reads.</param>
/// <param name="Title">Its title.</param>
/// <param name="Score">How well it matched; 0 in the page list.</param>
internal sealed record DocHitView(string Slug, string Title, double Score);
