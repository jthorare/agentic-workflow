namespace Aw.Sindri.Core.Events;

/// <summary>The 'profile.approvals.removed' payload (#2258 PR 1): the first start after approvals went removed the old approved-snapshot folder. Key 'profile.approvals.removed'.</summary>
/// <param name="Directory">The folder that was removed.</param>
public sealed record ProfileApprovalsRemoved(string Directory);
