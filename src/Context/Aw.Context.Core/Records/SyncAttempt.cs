namespace Aw.Context.Core.Records;

/// <summary>How one tick of a source's feed ended, for its persisted backoff (<see cref="IRecordStore.RecordAttempt"/>).</summary>
/// <param name="At">When the tick ended.</param>
/// <param name="Failed">True for a failure: the tick ended with a code other than the time cap's (a capped tick committed its pages, so it counts as progress).</param>
/// <param name="ErrorText">One scrubbed line saying what failed; null after a success.</param>
public sealed record SyncAttempt(DateTimeOffset At, bool Failed, string? ErrorText);
