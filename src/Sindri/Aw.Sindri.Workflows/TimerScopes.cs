using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The interpreter's timers (engine spec §5.5): each arming is one 'Workflow.DelayAsync' inside its own cancellation
/// scope, so cancelling the scope (a park, a takeover, leaving the entry) means the timer never fires, and every arming
/// is its own timer in history (rev 7's 'arm' counter is gone). One scope holds the state timer (the state's timeout,
/// the desk timeout, or the timer a 'then: retry' re-armed); a second holds 'takeoverIdle' while the instance is taken
/// over. A timer that ran out leaves its <see cref="DeadlineFired"/> for the loop's wait to take (<see cref="Take"/>);
/// the loop decides it like any other trigger. Expired timers fire on wake (X58). Workflow code only.
/// <para>
/// Engine P5 contract unit. Unit (a) owns the state scope (<see cref="Arm"/>, <see cref="Stop"/>, <see cref="Take"/>,
/// <see cref="FiresAt"/>) and its use in the desk and wait loops; unit (b) owns the takeover's <see cref="Pause"/> and
/// <see cref="Resume"/> and the 'takeoverIdle' scope. <see cref="Apply"/> is the one seam the loop calls with what
/// <see cref="StateTimers.Between"/> decided.
/// </para>
/// </summary>
/// <param name="actor">The actor of every <see cref="DeadlineFired"/> ('service:workflow.timers').</param>
internal sealed class TimerScopes(string actor)
{
    /// <summary>Does what <paramref name="action"/> says for the frame entry <paramref name="entry"/>.</summary>
    /// <param name="action">From StateTimers.Between.</param>
    /// <param name="entry">The frame entry the timer belongs to.</param>
    /// <returns>What a takeover left of the paused timer, for its suspension; null for every other action.</returns>
    public RemainingTimer? Apply(TimerAction action, int entry)
    {
        ArgumentNullException.ThrowIfNull(action);
        switch (action.Kind)
        {
            case TimerActionKind.Arm:
                Arm(action.Timer ?? DeadlineKind.State, entry, action.After ?? throw new ArgumentException("an arm needs its duration", nameof(action)));
                return null;
            case TimerActionKind.Stop:
                StopAll();
                return null;
            case TimerActionKind.Pause:
                return Pause(entry);
            case TimerActionKind.Resume:
                Resume(action is { Timer: { } kind, After: { } left } ? new RemainingTimer(kind, left) : null, entry);
                return null;
            case TimerActionKind.Keep:
            default:
                return null;
        }
    }

    private Action? _cancel;
    private DeadlineFired? _fired;
    private DateTimeOffset? _firesAt;
    private int _armed;
    private DeadlineKind _kind = DeadlineKind.State;
    private Action? _idleCancel;
    private DeadlineFired? _idleFired;
    private int _idleArmed;

    /// <summary>Whether a state timer, a retry timer or 'takeoverIdle' ran out and waits for <see cref="Take"/>: the loop's waits wake for it.</summary>
    public bool Fired => _fired is not null || _idleFired is not null;

    /// <summary>Cancels the running state scope, if any, and arms <paramref name="kind"/> to fire after <paramref name="after"/> in a new one. Unit (a).</summary>
    /// <param name="kind">The state timer or the retry timer.</param>
    /// <param name="entry">The frame entry it is armed in.</param>
    /// <param name="after">How long until it fires.</param>
    public void Arm(DeadlineKind kind, int entry, TimeSpan after)
    {
        Stop();
        var arming = ++_armed;
        _kind = kind;
        var span = after > TimeSpan.Zero ? after : TimeSpan.FromMilliseconds(1);
        _firesAt = new DateTimeOffset(Workflow.UtcNow, TimeSpan.Zero) + span;
        _ = RunAsync(new DeadlineFired(actor, entry, kind), span, arming);
    }

    /// <summary>Cancels the running state scope with no remainder (a park, an escalation, an end), and drops a fired timer not yet taken; 'takeoverIdle' is <see cref="StopAll"/>'s.</summary>
    public void Stop()
    {
        _armed++;   // a delay that completes in the same task as its cancel is from an old scope
        var cancel = _cancel;
        _cancel = null;
        _fired = null;
        _firesAt = null;
        cancel?.Invoke();
    }

    /// <summary>Cancels every scope: the state timer and 'takeoverIdle' (a cancel or a revoke while taken over, an end).</summary>
    public void StopAll()
    {
        Stop();
        StopIdle();
    }

    /// <summary>The timer that ran out, taken: a state or retry timer of the current entry, or 'takeoverIdle'; null when none has. Unit (a).</summary>
    /// <returns>The trigger for the loop to decide.</returns>
    public DeadlineFired? Take()
    {
        var fired = _fired ?? _idleFired;
        if (_fired is not null)
        {
            _fired = null;
        }
        else
        {
            _idleFired = null;
        }

        return fired;
    }

    /// <summary>When the running state timer fires, for status's 'timeout' line and the desk task; null when none runs. Unit (a).</summary>
    /// <returns>The time, from 'Workflow.UtcNow' at arming.</returns>
    public DateTimeOffset? FiresAt() => _firesAt;

    /// <summary>
    /// One arming: 'Workflow.DelayAsync' in its own cancellation scope (a timer in history). Cancelled, it never fires;
    /// run out, it leaves its trigger for the loop, unless a later arming or a stop has replaced it.
    /// </summary>
    private async Task RunAsync(DeadlineFired fired, TimeSpan after, int arming)
    {
        using var scope = CancellationTokenSource.CreateLinkedTokenSource(Workflow.CancellationToken);   // the workflow's own cancellation cancels the scope too
        _cancel = scope.Cancel;   // finding F3: Cancel, not CancelAsync, in workflow code
        try
        {
            await Workflow.DelayAsync(after, scope.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;   // the scope was cancelled: a park, a stop, a re-arm, or the workflow's own cancellation
        }
        finally
        {
            if (arming == _armed)
            {
                _cancel = null;   // the scope is disposed on leaving: a later Stop() must not reach it (a newer arming keeps its own)
            }
        }

        if (arming == _armed)
        {
            _firesAt = null;
            _fired = fired;
        }
    }

    /// <summary>
    /// A takeover (spec §5.5): cancels the running state scope, measures what was left from 'Workflow.UtcNow', and arms
    /// 'takeoverIdle' (2 h) in its own scope. A state timer that ran out but wasn't taken yet is kept with nothing left,
    /// so the handback fires it at once rather than losing it.
    /// </summary>
    /// <param name="entry">The frame entry.</param>
    /// <returns>The paused timer and what was left of it; null when none was running (a takeover over a park).</returns>
    public RemainingTimer? Pause(int entry)
    {
        var paused = (_firesAt, _fired) switch
        {
            ({ } at, _) => new RemainingTimer(_kind, Max(at - Now, TimeSpan.Zero)),
            (null, { } fired) => new RemainingTimer(fired.Kind, TimeSpan.Zero),
            _ => null,
        };
        Stop();
        ArmIdle(entry);
        return paused;
    }

    /// <summary>A handback (spec §5.5): cancels 'takeoverIdle' and re-arms <paramref name="remaining"/>, if a timer was paused.</summary>
    /// <param name="remaining">What the takeover left of the paused timer.</param>
    /// <param name="entry">The frame entry.</param>
    public void Resume(RemainingTimer? remaining, int entry)
    {
        StopIdle();
        if (remaining is { } left)
        {
            Arm(left.Kind, entry, left.Left);
        }
    }

    /// <summary>A start taken over (UI spec E-19): nothing runs yet; arms 'takeoverIdle' and keeps <paramref name="full"/> for the handback.</summary>
    /// <param name="full">The entered state's full timeout, if it has one.</param>
    /// <param name="entry">The frame entry.</param>
    /// <returns>What the handback re-arms.</returns>
    public RemainingTimer? Held(TimeSpan? full, int entry)
    {
        ArmIdle(entry);
        return full is { } after ? new RemainingTimer(DeadlineKind.State, after) : null;
    }

    private static DateTimeOffset Now => new(Workflow.UtcNow, TimeSpan.Zero);

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    /// <summary>Arms 'takeoverIdle' in its own scope: once it runs out its trigger waits for the loop, and nothing re-arms it (it notifies once and clears).</summary>
    private void ArmIdle(int entry)
    {
        StopIdle();
        var arming = ++_idleArmed;
        _ = IdleAsync(new DeadlineFired(actor, entry, DeadlineKind.TakeoverIdle), arming);
    }

    /// <summary>Cancels 'takeoverIdle' and drops its trigger if it ran out and wasn't taken.</summary>
    private void StopIdle()
    {
        _idleArmed++;
        var cancel = _idleCancel;
        _idleCancel = null;
        _idleFired = null;
        cancel?.Invoke();
    }

    private async Task IdleAsync(DeadlineFired fired, int arming)
    {
        using var scope = CancellationTokenSource.CreateLinkedTokenSource(Workflow.CancellationToken);   // the workflow's own cancellation cancels the scope too
        _idleCancel = scope.Cancel;   // finding F3: Cancel, not CancelAsync, in workflow code
        try
        {
            await Workflow.DelayAsync(StateTimers.TakeoverIdle, scope.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;   // the handback, a cancel, or the workflow's own cancellation
        }
        finally
        {
            if (arming == _idleArmed)
            {
                _idleCancel = null;   // the scope is disposed on leaving: a later StopIdle() must not reach it (a newer arming keeps its own)
            }
        }

        if (arming == _idleArmed)
        {
            _idleFired = fired;
        }
    }
}
