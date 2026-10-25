namespace Aw.Context.Core.Sources;

/// <summary>A fetched record, raw: the engine sanitizes, scrubs and trust-tags every field once (spec §5.1, §7).</summary>
/// <param name="Ref">The record.</param>
/// <param name="Kind">Its kind.</param>
/// <param name="Title">Its title.</param>
/// <param name="Text">Its text (whole; nothing is cut).</param>
/// <param name="Author">Its author, if the source has one.</param>
/// <param name="CreatedAt">When it was created, if known.</param>
/// <param name="UpdatedAt">When it last changed, if known.</param>
public sealed record SourceRecord(SourceRef Ref, RecordKind Kind, string Title, string Text, string? Author, DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt)
{
    /// <summary>
    /// True when the adapter already ran the ingest scrubber over the whole record and it was withheld (an adapter that
    /// cleans whole documents before cutting a span, like repo-docs). <see cref="Text"/> then holds no record text, and
    /// the engine cites the record as scrub-blocked.
    /// </summary>
    public bool Blocked { get; init; }
}
