using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The timer model the interpreter follows (engine spec §3.4, §5.5, W20, X58), pure so the workflow and its unit tests
/// call the same code. Every 'active' and 'wait' state arms its timeout as a 'Workflow.DelayAsync' inside its own
/// cancellation scope: the state timeout, the one 'then: retry' re-arms once in the same entry, and the desk timeout
/// (the state's timeout raised to 'workflow.deskTimeout', W20). A park or escalation stops it; a takeover pauses it,
/// keeping <see cref="RemainingTimer"/> on the suspension, and arms 'takeoverIdle'; a handback re-arms it with what was
/// left; an answer to a park arms the full timeout. Expired timers fire on wake (X58): Temporal's timers are server-side
/// wall clock. Engine P5 contract unit: unit (a) owns the state timers and 'retry', unit (b) the takeover's pause,
/// resume and 'takeoverIdle'.
/// </summary>
public static class StateTimers
{
    /// <summary>The takeover's idle reminder (spec §5.5): it notifies once ('hand back {item}?') and clears; it never hands back.</summary>
    public static TimeSpan TakeoverIdle { get; } = TimeSpan.FromHours(2);

    /// <summary>
    /// How long <paramref name="state"/>'s timer runs when armed in full (spec §3.4, W20): its 'timeout.after', raised to
    /// <paramref name="deskFloor"/> while a desk run is open; none for a terminal state. Unit (a).
    /// </summary>
    /// <param name="state">The recipe state.</param>
    /// <param name="run">The frame's open run, for an active state.</param>
    /// <param name="deskFloor">The profile's 'workflow.deskTimeout'.</param>
    /// <returns>The timeout; null when the state arms none.</returns>
    public static TimeSpan? Timeout(RecipeState state, OpenRun? run, TimeSpan deskFloor)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Kind == StateKind.Terminal || state.Timeout is not { } timeout)
        {
            return null;
        }

        return run is { Runtime: StepRuntime.Desk } ? DeskTimeouts.Wait(timeout, deskFloor) : timeout.After.Span;
    }

    /// <summary>
    /// What the interpreter does with its timers after a decided change from <paramref name="before"/> to
    /// <paramref name="after"/> (spec §5.5): a takeover's pause or a handback's resume (unit (b)) first, else what a new
    /// entry, a new run, a park, an answer or an end asks for (unit (a)); a handback that kept nothing resumes the full timeout.
    /// </summary>
    /// <param name="before">The instance before the change.</param>
    /// <param name="after">The instance after it.</param>
    /// <param name="recipe">The recipe, for the entered state's timeout.</param>
    /// <param name="deskFloor">The profile's 'workflow.deskTimeout'.</param>
    /// <returns>The action.</returns>
    public static TimerAction Between(WorkflowInstance before, WorkflowInstance after, Recipe recipe, TimeSpan deskFloor) =>
        Takeovers(before, after) ?? HandedBack(before, after, Entries(before, after, recipe, deskFloor));

    /// <summary>
    /// A handback whose takeover kept nothing (a start taken over, E-19) arms the entered state's full timeout; it is a
    /// resume, not a plain arm, so the interpreter also stops 'takeoverIdle', which only a resume cancels.
    /// </summary>
    private static TimerAction HandedBack(WorkflowInstance before, WorkflowInstance after, TimerAction entries) =>
        before.Suspension?.Kind == SuspensionKind.Takeover && after.Suspension?.Kind != SuspensionKind.Takeover && entries.Kind == TimerActionKind.Arm
            ? entries with { Kind = TimerActionKind.Resume }
            : entries;

    /// <summary>
    /// A takeover pauses the running timer (and arms 'takeoverIdle'); a handback resumes the timer its suspension kept,
    /// none when it returns to the park it covered, and leaves a takeover that kept nothing to the entries (the full
    /// timeout); a cancel while taken over stops every timer; a change that keeps the takeover keeps them; any other
    /// change is not a takeover's (null).
    /// </summary>
    internal static TimerAction? Takeovers(WorkflowInstance before, WorkflowInstance after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var was = before.Suspension?.Kind == SuspensionKind.Takeover;
        var now = after.Suspension?.Kind == SuspensionKind.Takeover;
        return (was, now) switch
        {
            (false, false) => null,                                  // not a takeover's change: the entries decide it
            (false, true) => TimerAction.Pause,                      // a takeover: pause what runs, arm takeoverIdle
            (true, true) => TimerAction.Keep,                        // still taken over: a notification, a pending park
            (true, false) when after.Ended => TimerAction.Stop,     // a cancel while taken over stops every timer
            (true, false) when after.Suspension is not null => new TimerAction(TimerActionKind.Resume, Timer: null, After: null),   // back to the park it covered
            (true, false) => before.Suspension!.Remaining is { } left // why: 'was' holds only with a suspension
                ? new TimerAction(TimerActionKind.Resume, left.Kind, left.Left)
                : null,                                              // nothing was kept: the entries arm the full timeout
        };
    }

    /// <summary>
    /// A new entry or run arms the entered state's full timeout; a park, escalation or end stops it; an answer that ends
    /// a park arms it in full ('rearm'); anything else keeps it. Unit (a).
    /// </summary>
    internal static TimerAction Entries(WorkflowInstance before, WorkflowInstance after, Recipe recipe, TimeSpan deskFloor)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(recipe);
        if (after.Ended || (after.Suspension is not null && (before.Suspension is null || NewRun(before, after))))
        {
            return TimerAction.Stop;   // a park, an escalation or an end; a new entry that parked at once runs no timer either
        }

        if (after.Suspension is not null)
        {
            return TimerAction.Keep;   // still suspended: the park's timer stays stopped
        }

        if (before.Suspension is not null || NewRun(before, after))
        {
            return Full(after, recipe, deskFloor, DeadlineKind.State);   // a new entry or run, or an answer that ended a park ('rearm')
        }

        return after.Frame.Retried && !before.Frame.Retried ? Full(after, recipe, deskFloor, DeadlineKind.Retry) : TimerAction.Keep;
    }

    /// <summary>A new entry, or a new run in the same entry.</summary>
    private static bool NewRun(WorkflowInstance before, WorkflowInstance after) =>
        before.Frame.Entry != after.Frame.Entry || before.Frame.Run?.Token != after.Frame.Run?.Token;

    /// <summary>Arms <paramref name="timer"/> with the state's full timeout; stops the timer of a state that has none.</summary>
    private static TimerAction Full(WorkflowInstance instance, Recipe recipe, TimeSpan deskFloor, DeadlineKind timer) =>
        recipe.States.TryGetValue(instance.Frame.State, out var state) && Timeout(state, instance.Frame.Run, deskFloor) is { } after
            ? new TimerAction(TimerActionKind.Arm, timer, after)
            : TimerAction.Stop;
}
