namespace Aw.Sindri.Core.Graph.Docs;

/// <summary>The workflow docs shipped in the binary: concept pages and one page per block.</summary>
public interface IWorkflowDocs
{
    /// <summary>Every page, by slug.</summary>
    public IReadOnlyList<WorkflowDocPage> Pages { get; }
}
