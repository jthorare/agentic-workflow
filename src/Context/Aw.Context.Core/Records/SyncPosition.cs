using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Records;

/// <summary>A source's persisted place in its feed (the 'source_cursor' row, spec §5.5).</summary>
/// <param name="Cursor">The change cursor, or null before the first page.</param>
/// <param name="Phase">The phase the next pull belongs to, or null before the first page.</param>
/// <param name="BackfillCursor">Where the backfill resumes, or null.</param>
/// <param name="BackfillBefore">Where Recent started, stored when Recent ended: the exclusive upper bound of every Backfill pull (#1512); null before then.</param>
public sealed record SyncPosition(SourceCursor? Cursor, SyncPhase? Phase, SourceCursor? BackfillCursor, DateTimeOffset? BackfillBefore = null)
{
    /// <summary>A source that has never committed a page.</summary>
    public static SyncPosition Start { get; } = new(Cursor: null, Phase: null, BackfillCursor: null);
}
