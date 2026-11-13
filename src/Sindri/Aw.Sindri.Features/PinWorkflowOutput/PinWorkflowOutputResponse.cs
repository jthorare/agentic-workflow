using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.PinWorkflowOutput;

/// <summary>The output is pinned.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Revision">The revision it was pinned on.</param>
/// <param name="Node">The step.</param>
/// <param name="PinnedBy">Who pinned it.</param>
internal sealed record PinWorkflowOutputResponse(WorkflowSlug Slug, int Revision, string Node, string PinnedBy);
