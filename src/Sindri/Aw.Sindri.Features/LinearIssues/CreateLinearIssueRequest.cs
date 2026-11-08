using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>Create a Linear issue (POST /sindri/linear/issues; Linear hub spec 2026-11-04 §3, §5.2): desk class, the owner's, an agent session is refused (SND-AUTH-002). Every id is checked against the team's stored lookups (SND-LINEAR-006) before anything is sent.</summary>
/// <param name="ClientId">The app's uuid; a retry sends the same one, so the issue is never created twice.</param>
/// <param name="Team">The team's key, 'ENG'; it must be one the profile picked (SND-LINEAR-003).</param>
/// <param name="Title">The title, 1 to 255 characters.</param>
/// <param name="Description">The description as markdown, up to 65,536 characters, or null.</param>
/// <param name="StateId">A state of the team, or null for Linear's default.</param>
/// <param name="AssigneeId">A member of the team, or null for none.</param>
/// <param name="Priority">0 (none) to 4 (low), or null for Linear's default.</param>
/// <param name="ProjectId">A project's Linear id, or null.</param>
/// <param name="CycleId">A cycle of the team, or null.</param>
/// <param name="LabelIds">Labels of the team; null or empty for none.</param>
/// <param name="ParentKey">The parent issue's key, 'ENG-1', or null.</param>
internal sealed record CreateLinearIssueRequest(
    string ClientId,
    string Team,
    string Title,
    string? Description = null,
    string? StateId = null,
    string? AssigneeId = null,
    int? Priority = null,
    string? ProjectId = null,
    string? CycleId = null,
    IReadOnlyList<string>? LabelIds = null,
    string? ParentKey = null) : ICommand<CreateLinearIssueResponse>;
