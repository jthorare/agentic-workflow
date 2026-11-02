namespace Aw.Sindri.Core.Projects.Facts;

/// <summary>One stored Linear project and its counts (projects page spec §6.1). Text in it came from Linear: untrusted, fenced for a session.</summary>
/// <param name="Key">'linear:project/&lt;id&gt;'.</param>
/// <param name="Title">Linear's name.</param>
/// <param name="Description">Linear's description, or null.</param>
/// <param name="Url">The project's Linear url ('https://linear.app/&lt;ws&gt;/project/&lt;name&gt;-&lt;slug&gt;').</param>
/// <param name="StateRaw">Linear's status type: backlog, planned, started, paused, completed or canceled; null when unknown.</param>
/// <param name="Health">Linear's newest update's health: onTrack, atRisk or offTrack; null when none.</param>
/// <param name="DueAt">Linear's target date, or null.</param>
/// <param name="Lead">The project's lead, or null.</param>
/// <param name="LeadIsOwner">Whether the lead is the owner.</param>
/// <param name="UpdatedAt">When Linear last changed the project.</param>
/// <param name="ClosedAt">Linear's completedAt or canceledAt, or null.</param>
/// <param name="OpenIssues">Stored open issues in the project (exact, spec P8).</param>
/// <param name="Counting">The one-time issue re-read (spec §4.3) hasn't finished, so the count isn't final.</param>
/// <param name="LastActivity">The newest change of the project or its issues, or null.</param>
public sealed record LinearProjectFact(
    string Key,
    string Title,
    string? Description,
    string? Url,
    string? StateRaw,
    string? Health,
    DateTimeOffset? DueAt,
    ProjectPerson? Lead,
    bool LeadIsOwner,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    int OpenIssues,
    bool Counting,
    DateTimeOffset? LastActivity);
