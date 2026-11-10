namespace Aw.Sindri.Features.SearchWorkflowDocs;

/// <summary>One doc page in full.</summary>
/// <param name="Slug">The page's slug.</param>
/// <param name="Title">Its title.</param>
/// <param name="Text">Its Markdown, examples included.</param>
internal sealed record DocPageView(string Slug, string Title, string Text);
