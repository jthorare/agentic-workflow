using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The payload of 'workflow.created', 'workflow.revised', 'workflow.activated', 'workflow.paused' and 'workflow.archived' (issue 2258, PR 8): which workflow changed, how, and who did it.</summary>
/// <param name="WorkflowId">The workflow.</param>
/// <param name="Slug">Its slug.</param>
/// <param name="Revision">The revision the change made, activated or left active; for a pause or an archive, the latest revision.</param>
/// <param name="SpecHash">The revision's spec hash, or null for a pause or an archive.</param>
/// <param name="By">Who did it: the caller's actor, as the command context carries it.</param>
public sealed record WorkflowChangedPayload(WorkflowId WorkflowId, WorkflowSlug Slug, int Revision, string? SpecHash, string By);
