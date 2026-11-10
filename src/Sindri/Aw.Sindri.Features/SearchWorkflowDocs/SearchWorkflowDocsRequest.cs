using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.SearchWorkflowDocs;

/// <summary>Search or read the workflow docs (GET /sindri/workflows/docs?question=&amp;page=, 'sindri wf docs', MCP workflow_docs). A question ranks the pages; a page reads one in full; neither lists every page.</summary>
/// <param name="Question">What the caller wants to know, in words, or null.</param>
/// <param name="Page">A page slug to read in full (such as 'block-schedule'), or null.</param>
internal sealed record SearchWorkflowDocsRequest(string? Question, string? Page) : IQuery<SearchWorkflowDocsResponse>;
