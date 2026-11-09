using Aw.Sindri.Core.Graph.Docs;

namespace Aw.Sindri.Infrastructure.WorkflowDocs;

/// <summary>The workflow doc pages embedded from docs/workflows/*.md (resource names 'workflow-docs/&lt;slug&gt;.md').</summary>
public static class EmbeddedWorkflowDocs
{
    /// <summary>Reads every embedded page.</summary>
    /// <returns>The pages, ordered by slug.</returns>
    public static IReadOnlyList<WorkflowDocPage> Load() => throw new NotSupportedException();
}
