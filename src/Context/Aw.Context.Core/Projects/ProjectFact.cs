using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Projects;

/// <summary>One stored project with the counts the page shows (projects page spec §6.1).</summary>
/// <param name="Key">The record key, such as 'linear:project/&lt;id&gt;'.</param>
/// <param name="Title">The project's name.</param>
/// <param name="Description">The project's description; null until the store keeps one apart from the record text.</param>
/// <param name="Url">The project's link in Linear.</param>
/// <param name="StateRaw">Linear's status type word ('started', 'paused', 'completed' ...).</param>
/// <param name="Health">The latest health word ('onTrack', 'atRisk', 'offTrack'), or null.</param>
/// <param name="DueAt">The target day, at midnight UTC, or null.</param>
/// <param name="Lead">Who leads it, or null.</param>
/// <param name="LeadIsOwner">The lead is the person the Linear key belongs to.</param>
/// <param name="UpdatedAt">When the project last changed (the sync time when Linear gave none).</param>
/// <param name="ClosedAt">When it completed or was canceled.</param>
/// <param name="OpenIssues">Open issues linked to it that the caller may see.</param>
/// <param name="OpenPrs">Open pull requests of its issues; null until ship 2.</param>
/// <param name="LastActivity">The newest change of the project or any of its issues.</param>
/// <param name="Repos">The repos its pull requests are in; null until ship 2.</param>
/// <param name="Counting">The issues are still being re-read for project links, so <paramref name="OpenIssues"/> may be low.</param>
public sealed record ProjectFact(
    RecordKey Key,
    string Title,
    string? Description,
    string? Url,
    string? StateRaw,
    string? Health,
    DateTimeOffset? DueAt,
    UserRef? Lead,
    bool LeadIsOwner,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    int OpenIssues,
    int? OpenPrs,
    DateTimeOffset? LastActivity,
    IReadOnlyList<ContextRepoName>? Repos,
    bool Counting);
