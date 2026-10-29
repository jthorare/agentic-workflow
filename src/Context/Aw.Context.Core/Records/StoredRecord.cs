using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Records;

/// <summary>One row of 'source_record' with its links (spec §4.2): the structured facts a tracker gives, scrubbed, never the raw text.</summary>
/// <param name="Key">The record's key.</param>
/// <param name="Source">The source it came from.</param>
/// <param name="Kind">Its kind.</param>
/// <param name="Repo">The profile repo it belongs to, or null.</param>
/// <param name="Audience">Who may see it; null means no session.</param>
/// <param name="Title">Its scrubbed one-line title (the key when the record is blocked or removed).</param>
/// <param name="Url">Its link, allowed schemes and hosts only.</param>
/// <param name="Author">Its scrubbed author.</param>
/// <param name="State">Its normalised state.</param>
/// <param name="StateRaw">The source's own word for the state, scrubbed.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="UpdatedAt">When it last changed.</param>
/// <param name="ClosedAt">When it closed.</param>
/// <param name="Version">The source's version: updatedAt, or a blob id for a doc.</param>
/// <param name="TextHash">The sha-256 of the scrubbed full text.</param>
/// <param name="Ruleset">The scrub ruleset it was cleaned with.</param>
/// <param name="Blocked">The scrubber withheld the text: the row stays and has no chunks.</param>
/// <param name="RemovedAt">When the source deleted it (a tombstone), or null.</param>
/// <param name="SyncedAt">When the store last wrote it.</param>
/// <param name="Links">What the source says connects it to other records.</param>
/// <param name="Health">A project's latest health word as the source gave it ('onTrack', 'atRisk', 'offTrack'), or null.</param>
/// <param name="DueAt">The day a project is due, or null.</param>
public sealed record StoredRecord(
    RecordKey Key,
    SourceId Source,
    RecordKind Kind,
    ContextRepoName? Repo,
    AudienceKey? Audience,
    string Title,
    string? Url,
    string? Author,
    RecordState? State,
    string? StateRaw,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ClosedAt,
    SourceVersion Version,
    string TextHash,
    string Ruleset,
    bool Blocked,
    DateTimeOffset? RemovedAt,
    DateTimeOffset SyncedAt,
    IReadOnlyList<RecordLink> Links,
    string? Health = null,
    DateOnly? DueAt = null);
