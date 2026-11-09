using Aw.Sindri.Core.Graph.Docs;

namespace Aw.Sindri.Infrastructure.WorkflowDocs;

/// <summary>The docs embedded in this binary, read once on first use.</summary>
internal sealed class ShippedWorkflowDocs : IWorkflowDocs
{
    private readonly Lazy<IReadOnlyList<WorkflowDocPage>> _pages = new(EmbeddedWorkflowDocs.Load);

    public IReadOnlyList<WorkflowDocPage> Pages => _pages.Value;
}
