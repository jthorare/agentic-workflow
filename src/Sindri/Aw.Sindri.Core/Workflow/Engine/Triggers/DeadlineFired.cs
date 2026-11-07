using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// A timer the interpreter armed fired (spec §5.5): the state's timeout, the one a 'then: retry' re-armed in the same
/// entry, or the takeover's idle reminder. A state or retry timer runs the state's 'then'; a takeoverIdle timer
/// notifies the human once (exempt from the approval check). The interpreter cancels a state's timer when the instance
/// is suspended or leaves the entry, so one only fires for the entry it was armed in.
/// </summary>
/// <param name="Actor">'service:workflow.timers'.</param>
/// <param name="Entry">The frame entry the timer was armed in.</param>
/// <param name="Kind">Which timer.</param>
public sealed record DeadlineFired(string Actor, int Entry, DeadlineKind Kind) : Trigger(Actor)
{
    /// <summary>For a skill Step whose result file was unreadable at its last turn end: why (scrubbed), so the timeout's park or escalation says so; null for every other timer.</summary>
    public string? Detail { get; init; }

    /// <inheritdoc/>
    public override string Name => Kind == DeadlineKind.TakeoverIdle ? TriggerNames.TakeoverIdle : TriggerNames.Deadline;

    /// <inheritdoc/>
    public override string KeyPart => string.Create(CultureInfo.InvariantCulture, $"timer:{Entry}:{Kind switch
    {
        DeadlineKind.Retry => "retry",
        DeadlineKind.TakeoverIdle => "takeoverIdle",
        DeadlineKind.State or _ => "state",
    }}");

    /// <inheritdoc/>
    public override bool ExemptFromApproval => Kind == DeadlineKind.TakeoverIdle;
}
