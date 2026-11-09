namespace Aw.Sindri.Features.SearchWorkflowDocs;

/// <summary>Ranked pages, the page list, or one page.</summary>
/// <param name="Hits">For a question, the best pages first; for no question and no page, every page with a score of 0; empty when a page was read.</param>
/// <param name="Page">The page read in full, or null.</param>
internal sealed record SearchWorkflowDocsResponse(IReadOnlyList<DocHitView> Hits, DocPageView? Page);
