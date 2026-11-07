using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>A stored Linear issue as a page reads it: the record's own columns and the fields of its 'linear_issue' row (Linear hub spec 2026-11-04 §4.1). The comments are read apart; the description is here.</summary>
/// <param name="Key">The record's key, 'linear:issue/&lt;uuid&gt;'.</param>
/// <param name="Title">The record's title, scrubbed; the key when the scrubber withheld it.</param>
/// <param name="Url">Its link on Linear.</param>
/// <param name="StateRaw">Linear's own word for the state ('In Review').</param>
/// <param name="Author">The creator's name.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="ClosedAt">When it was completed or canceled.</param>
/// <param name="TeamKey">The team's key ('ENG'), from the record's audience; null when the audience isn't a Linear team.</param>
/// <param name="Fields">The fields.</param>
/// <param name="Description">The description, scrubbed and capped; the withheld marker when the scrubber blocked it; null when the issue has none.</param>
public sealed record LinearIssueRow(RecordKey Key, string Title, string? Url, string? StateRaw, string? Author, DateTimeOffset? CreatedAt, DateTimeOffset? ClosedAt, string? TeamKey, LinearIssueFields Fields, string? Description = null);
