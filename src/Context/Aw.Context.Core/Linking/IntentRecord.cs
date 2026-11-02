using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linking;

/// <summary>An intent record the sync knows (spec §6.2): never its text, only what identifies it. The title is untrusted and scrubbed.</summary>
/// <param name="Key">The record's canonical key.</param>
/// <param name="Source">The source it came from.</param>
/// <param name="Version">The source's version of the record (a Linear record's updatedAt).</param>
/// <param name="Title">The scrubbed one-line title (untrusted).</param>
/// <param name="Aliases">Other names for it, such as the tracker key 'SPL-103'.</param>
public sealed record IntentRecord(RecordKey Key, SourceId Source, string Version, string Title, IReadOnlyList<string> Aliases)
{
    /// <summary>When the source created the record, or null (a row stored before the column existed, or a source that gives none). A 'before' read treats null as leaky.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>When the sync last wrote the record (its version moved); set by the store when it reads the row back, null on a record that hasn't been stored.</summary>
    public DateTimeOffset? FetchedAt { get; init; }
}
