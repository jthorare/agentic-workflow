namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// What the interpreter does with its timer scopes after one decided change (engine spec §5.5), as the pure
/// <see cref="StateTimers.Between"/> reads it from the instance before and after. Every arming is its own
/// 'Workflow.DelayAsync' in its own cancellation scope, so rev 7's 'arm' counter is gone. Engine P5 contract unit.
/// </summary>
/// <param name="Kind">What to do.</param>
/// <param name="Timer">The timer to arm for <see cref="TimerActionKind.Arm"/> and <see cref="TimerActionKind.Resume"/>: the state's, or the retry's.</param>
/// <param name="After">How long until it fires: the state's timeout (raised to 'workflow.deskTimeout' for a desk run), or what a takeover left of it.</param>
public sealed record TimerAction(TimerActionKind Kind, DeadlineKind? Timer, TimeSpan? After)
{
    /// <summary>Nothing changes.</summary>
    public static TimerAction Keep { get; } = new(TimerActionKind.Keep, Timer: null, After: null);

    /// <summary>The scope is cancelled with no remainder.</summary>
    public static TimerAction Stop { get; } = new(TimerActionKind.Stop, Timer: null, After: null);

    /// <summary>A takeover pauses the running timer and arms 'takeoverIdle'.</summary>
    public static TimerAction Pause { get; } = new(TimerActionKind.Pause, Timer: null, After: null);
}
