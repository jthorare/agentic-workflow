using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Records;

/// <summary>One stored project as <see cref="IRecordStore.ProjectFacts"/> reads it: the record's own fields, its lead and the counts over the issues linked to it.</summary>
/// <param name="Key">The project's key.</param>
/// <param name="Title">Its scrubbed name.</param>
/// <param name="Url">Its link.</param>
/// <param name="StateRaw">The source's status word.</param>
/// <param name="Health">The latest health word.</param>
/// <param name="DueAt">The target day.</param>
/// <param name="UpdatedAt">When it last changed; the sync time when the source gave none.</param>
/// <param name="ClosedAt">When it closed.</param>
/// <param name="LeadRef">The 'led-by' link's target (such as 'linear:user/&lt;id&gt;'), or null.</param>
/// <param name="LeadName">The lead's name as the link states it.</param>
/// <param name="OpenIssues">Issues linked to it ('in-project') that are open, not removed and visible to the caller.</param>
/// <param name="NewestIssueAt">The newest 'UpdatedAt' among its visible, not removed linked issues, or null when it has none.</param>
public sealed record ProjectRow(
    RecordKey Key,
    string Title,
    string? Url,
    string? StateRaw,
    string? Health,
    DateOnly? DueAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    string? LeadRef,
    string? LeadName,
    int OpenIssues,
    DateTimeOffset? NewestIssueAt);
