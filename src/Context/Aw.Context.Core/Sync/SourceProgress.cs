using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Sync;

/// <summary>
/// Where one record source's sync stands (owned-index spec §5.8): the phase its next pull belongs to, when the last tick ran and the code it ended with, and the two
/// milestones the owner sees. <b>Usable</b> is the recent window synced (the phase moved past Recent). <b>Complete</b> is, for now, only "backfill done" (the phase
/// moved past Backfill); it will also mean "embedding done" once the embed job exists.
/// </summary>
/// <param name="Source">The source.</param>
/// <param name="Phase">The stored phase, or null for a source that never committed a page.</param>
/// <param name="LastSync">When the last tick of this feed ended since the process started, or null when none has.</param>
/// <param name="LastCode">The code the last tick ended with (a down source's SND-CONTEXT-010, the time cap's SND-CONTEXT-059), or null when it completed or none has run.</param>
/// <param name="Usable">The recent window is synced.</param>
/// <param name="Complete">The backfill is done (the embeddings are not counted yet).</param>
/// <param name="ReReading">How many records the source already holds while it re-reads them (Recent, no completed read behind it, rows stored: the Linear issue feed after migration 0020's reset, projects page spec §4.3), or null when it is not re-reading.</param>
/// <param name="Pull">'running' while the source's sync lock is held, 'backoff' while failing before its next try, 'failed' after five failures in a row once past it, else 'idle'; null where the caller did not read it.</param>
/// <param name="Records">How many records the store holds for the source; null where the caller did not count.</param>
/// <param name="LastCommit">The end of the last tick that did not fail, or null when none did.</param>
/// <param name="LastError">The code of the last failure (the stored last error), or null after a success.</param>
/// <param name="LastErrorText">One scrubbed line saying what the last failure was.</param>
/// <param name="Failures">Failed ticks in a row.</param>
/// <param name="NextTry">When a source in backoff is pulled again by a scheduled tick.</param>
public sealed record SourceProgress(SourceId Source, SyncPhase? Phase, DateTimeOffset? LastSync, string? LastCode, bool Usable, bool Complete, int? ReReading = null,
    string? Pull = null, int? Records = null, DateTimeOffset? LastCommit = null, string? LastError = null, string? LastErrorText = null, int? Failures = null, DateTimeOffset? NextTry = null)
{
    /// <summary>The progress of a source from its stored phase: usable once Recent is through, complete once Backfill is through.</summary>
    /// <param name="source">The source.</param>
    /// <param name="phase">Its stored phase (the one the next pull belongs to), or null before the first page.</param>
    /// <param name="lastSync">When its last tick ended, or null.</param>
    /// <param name="lastCode">The code that tick ended with, or null.</param>
    /// <param name="stored">How many records the source holds, when the caller checks for a re-read; null: not checked.</param>
    /// <returns>The progress row. A source in Recent that already holds records is re-reading (a first read starts with none).</returns>
    public static SourceProgress From(SourceId source, SyncPhase? phase, DateTimeOffset? lastSync, string? lastCode, int? stored = null) =>
        new(source, phase, lastSync, lastCode, Usable: phase is SyncPhase.Backfill or SyncPhase.Incremental or SyncPhase.Reconcile, Complete: phase is SyncPhase.Incremental or SyncPhase.Reconcile,
            ReReading: phase is SyncPhase.Recent && stored is > 0 ? stored : null);
}
