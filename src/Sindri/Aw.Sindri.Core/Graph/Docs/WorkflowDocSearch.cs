namespace Aw.Sindri.Core.Graph.Docs;

/// <summary>Keyword search over the workflow doc pages: ranks by the question's words in titles, headings and text. No embeddings.</summary>
public static class WorkflowDocSearch
{
    /// <summary>Finds the pages that best answer <paramref name="question"/>.</summary>
    /// <param name="pages">The pages to search.</param>
    /// <param name="question">The question, in plain words.</param>
    /// <param name="max">The most hits to return.</param>
    /// <returns>The pages with a match, best first; ties go by page slug.</returns>
    public static IReadOnlyList<WorkflowDocHit> Search(IReadOnlyList<WorkflowDocPage> pages, string question, int max = 5) => throw new NotSupportedException();
}
