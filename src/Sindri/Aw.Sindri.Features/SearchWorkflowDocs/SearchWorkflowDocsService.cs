using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Docs;

namespace Aw.Sindri.Features.SearchWorkflowDocs;

/// <summary>The workflow docs: a page by slug (an unknown one is SND-GRAPH-008 and names the slugs), ranked pages for a question, or the list of pages. Giving both a question and a page is invalid.</summary>
/// <param name="docs">The pages shipped in the binary.</param>
internal sealed class SearchWorkflowDocsService(IWorkflowDocs docs) : IService<SearchWorkflowDocsRequest, SearchWorkflowDocsResponse>
{
    public ValueTask<Result<SearchWorkflowDocsResponse>> Handle(SearchWorkflowDocsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(Answer(request));
    }

    private Result<SearchWorkflowDocsResponse> Answer(SearchWorkflowDocsRequest request)
    {
        var question = string.IsNullOrWhiteSpace(request.Question) ? null : request.Question;
        if (question is not null && request.Page is not null)
        {
            return SindriErrors.InvalidInput.With("give a question or a page, not both");
        }

        if (request.Page is { } slug)
        {
            var page = docs.Pages.FirstOrDefault(p => string.Equals(p.Slug, slug, StringComparison.Ordinal));
            return page is null
                ? SindriErrors.GraphNotFound.With($"no doc page '{slug}'; the pages are {string.Join(", ", docs.Pages.Select(static p => p.Slug))}")
                : new SearchWorkflowDocsResponse([], new DocPageView(page.Slug, page.Title, page.Text));
        }

        IReadOnlyList<DocHitView> hits = question is null
            ? [.. docs.Pages.Select(static p => new DocHitView(p.Slug, p.Title, 0))]
            : [.. WorkflowDocSearch.Search(docs.Pages, question).Select(static h => new DocHitView(h.Page.Slug, h.Page.Title, h.Score))];
        return new SearchWorkflowDocsResponse(hits, Page: null);
    }
}
