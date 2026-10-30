using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Sync;

/// <summary>One record as a feed hands it over: raw, before the driver scrubs and chunks it (the shape of 'ConnectorItem', spec §5.2).</summary>
/// <param name="Key">The record's key.</param>
/// <param name="Kind">Its kind.</param>
/// <param name="Title">Its title.</param>
/// <param name="Text">Its whole text.</param>
/// <param name="Author">Its author.</param>
/// <param name="Url">Its link as the source gave it.</param>
/// <param name="State">Its normalised state.</param>
/// <param name="StateRaw">The source's own word for it.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="UpdatedAt">When it last changed.</param>
/// <param name="ClosedAt">When it closed.</param>
/// <param name="Version">The source's version.</param>
/// <param name="Links">What the source says connects it to other records.</param>
/// <param name="Repo">The profile repo it belongs to, or null.</param>
/// <param name="Audience">Who may see it; the feed knows the team or channel it read.</param>
/// <param name="Health">A project's latest health word as the source gave it, or null.</param>
/// <param name="DueAt">The day a project is due, or null.</param>
/// <param name="Description">A project's description as the source gave it (free text), or null; the driver scrubs and caps it.</param>
public sealed record RecordItem(
    RecordKey Key,
    RecordKind Kind,
    string Title,
    string Text,
    string? Author,
    string? Url,
    RecordState? State,
    string? StateRaw,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ClosedAt,
    SourceVersion Version,
    IReadOnlyList<RecordLink> Links,
    ContextRepoName? Repo,
    AudienceKey? Audience,
    string? Health = null,
    DateOnly? DueAt = null,
    string? Description = null);
