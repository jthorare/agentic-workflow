namespace Aw.Context.Infrastructure.Linear;

/// <summary>A project as the ProjectsPull operation serves it: its status, dates, teams and newest update.</summary>
/// <param name="Id">The Id Linear serves.</param>
/// <param name="Name">The Name Linear serves.</param>
/// <param name="UpdatedAt">The UpdatedAt Linear serves.</param>
/// <param name="Description">The Description Linear serves.</param>
/// <param name="Url">The Url Linear serves.</param>
/// <param name="CreatedAt">The CreatedAt Linear serves.</param>
/// <param name="CompletedAt">The CompletedAt Linear serves.</param>
/// <param name="CanceledAt">The CanceledAt Linear serves.</param>
/// <param name="TargetDate">The TargetDate Linear serves ('yyyy-MM-dd').</param>
/// <param name="Status">The Status Linear serves.</param>
/// <param name="State">The legacy State word, read when the workspace serves no Status.</param>
/// <param name="Lead">The Lead Linear serves.</param>
/// <param name="Teams">The Teams Linear serves, by key.</param>
/// <param name="LastUpdate">The LastUpdate Linear serves.</param>
internal sealed record LinearProjectNode(string Id, string Name, DateTimeOffset UpdatedAt, string? Description = null, string? Url = null, DateTimeOffset? CreatedAt = null, DateTimeOffset? CompletedAt = null, DateTimeOffset? CanceledAt = null, string? TargetDate = null, LinearProjectStatus? Status = null, string? State = null, LinearNamed? Lead = null, LinearConnection<LinearTeamKey>? Teams = null, LinearProjectHealth? LastUpdate = null);
