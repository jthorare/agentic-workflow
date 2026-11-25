namespace Aw.Meta.Core.Events;

/// <summary>One event in aw-meta's append-only event log: something that happened to the team, read by a cursor (docs/csharp/meta.md "Events").</summary>
/// <param name="Id">Its number, rising across the log; ignored on append (the store assigns it).</param>
/// <param name="At">When it happened.</param>
/// <param name="Kind">Dotted lower-case, such as obligation.opened.</param>
/// <param name="Lane">The lane it concerns, or null for a team-wide or box-wide event.</param>
/// <param name="Fields">The event's fields as one JSON object.</param>
public sealed record MetaEvent(long Id, DateTimeOffset At, string Kind, string? Lane, string Fields)
{
    /// <summary>How long an event is kept: an append drops the events older than this before its own time.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);
}
