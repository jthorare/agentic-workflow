namespace Aw.Context.Core.Records;

/// <summary>A record source's persisted backoff and last commit, kept on its feed's `source_cursor` row so a restart keeps them.</summary>
/// <param name="Failures">Failed ticks in a row.</param>
/// <param name="NextTry">When a failing source may be pulled again by a scheduled tick; null when it is not backing off.</param>
/// <param name="LastCommit">The end of the last tick that did not fail, or null when none did.</param>
/// <param name="LastErrorText">One scrubbed line saying what the last failure was; null after a success.</param>
public sealed record SourceBackoffState(int Failures, DateTimeOffset? NextTry, DateTimeOffset? LastCommit, string? LastErrorText)
{
    /// <summary>A source that never ticked.</summary>
    public static SourceBackoffState None { get; } = new(Failures: 0, NextTry: null, LastCommit: null, LastErrorText: null);
}
