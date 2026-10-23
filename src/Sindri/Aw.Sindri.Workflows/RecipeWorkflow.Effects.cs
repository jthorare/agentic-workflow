using Aw.Redaction;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The interpreter's effects (engine spec rev 8 §5.7, §3.4; engine P6 unit (a)). After a change's ledger event the loop
/// hands its effects to <see cref="Emit"/>, which starts <see cref="Dispatch"/> as a workflow task it doesn't await, so
/// the instance enters its next state while a recipe effect waits out the dispatch delay. An effect identity runs once
/// per instance: one already dispatched, or still on its way, is skipped, so a 'retry' re-emission (the retry decision's
/// effects, which reach <see cref="Emit"/> through <c lang="csharp">TransitionDecision.Retry</c>) writes nothing twice. A cancel within
/// the delay aborts the effect. The instance returns only once every effect it
/// emitted is dispatched, refused or aborted.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    private const string Dispatcher = "system:dispatcher";
    private readonly List<Task> _effects = [];
    private readonly HashSet<string> _effectsOpen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _effectsSent = new(StringComparer.Ordinal);
    private bool? _replyFacts;
    private bool? _noticesSurviveCancel;
    private CancellationTokenSource? _effectsCancel;
    private TimeSpan _dispatchDelay;
    private bool _deskTaskNotified;

    /// <summary>
    /// Runs <paramref name="effects"/> of the change that left the instance as <paramref name="after"/>: each through
    /// 'RecordEffect' (pending); a recipe effect then waits 'workflow.dispatchDelay' (cancelled if the instance is) and
    /// passes 'RecheckEffect', or is refused ('RecordEffect' refused, then an 'OutboxRefused' trigger); then
    /// 'RunEffect'. An engine notice skips the delay and the recheck: a park interrupts at once.
    /// </summary>
    /// <param name="after">The instance after the change.</param>
    /// <param name="effects">The effects to run.</param>
    /// <returns>Done once every effect is dispatched, refused or aborted.</returns>
    internal Task Dispatch(WorkflowInstance after, IReadOnlyList<EffectCommand> effects)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(effects);
        List<Task> running = [];
        List<EffectCommand> later = [];
        foreach (var effect in effects)
        {
            if (IsLineReport(effect))
            {
                later.Add(effect);   // LN-WF1: the report follows the others, so two effects of one change never race for the history's order
            }
            else if (!_effectsSent.Contains(effect.Identity.Text) && _effectsOpen.Add(effect.Identity.Text))
            {
                running.Add(DispatchOne(after, effect));
            }
        }

        return later.Count == 0 ? Workflow.WhenAllAsync(running) : ThenReport(after, running, later);
    }

    private async Task ThenReport(WorkflowInstance after, List<Task> first, List<EffectCommand> reports)
    {
        await Workflow.WhenAllAsync(first).ConfigureAwait(true);
        foreach (var report in reports.Where(r => !_effectsSent.Contains(r.Identity.Text) && _effectsOpen.Add(r.Identity.Text)))
        {
            await DispatchOne(after, report).ConfigureAwait(true);
        }
    }

    private static bool IsLineReport(EffectCommand effect) => effect.Origin == CommandOrigin.Engine && effect.Effect.Value == "effect.line.report";

    /// <summary>
    /// Starts the effects of a change just mirrored: the recipe's effects for this instance and the engine's own notices.
    /// A change that cancels the instance aborts every effect still in its delay.
    /// </summary>
    /// <param name="before">The instance before the change (null at start).</param>
    /// <param name="after">The instance after it.</param>
    /// <param name="effects">The recipe effects the change runs.</param>
    /// <param name="change">The type of the change's ledger event: a 'workflow.notified' one is takeoverIdle's (P5).</param>
    private void Emit(WorkflowInstance? before, WorkflowInstance after, IReadOnlyList<EffectCommand> effects, WorkflowEventType change = WorkflowEventType.Transition)
    {
        Announce(before, after);   // engine P7: a child tells its parent about a park, escalation or resume
        List<EffectCommand> all = [.. effects.Where(e => e.Identity.Instance == after.Id), .. Notices(before, after, change), .. LineReports(before, after)];
        if (all.Count > 0)
        {
            _effects.Add(Dispatch(after, all));
        }

        if (after.Ended && string.Equals(after.Frame.State, EngineStates.Cancelled, StringComparison.Ordinal))
        {
            Effects().Cancel();
        }
    }

    /// <summary>The effects a decision runs (§5.7): an applied or refused trigger's.</summary>
    private static IReadOnlyList<EffectCommand> EffectsOf(Decision? decision) => decision switch
    {
        Applied applied => applied.Effects,
        Refused refused => refused.Effects,
        _ => [],
    };

    /// <summary>
    /// The engine's own notices for a change (§5.7 origin 'engine'): parks, escalations, takeoverIdle and desk tasks.
    /// </summary>
    private IReadOnlyList<EffectCommand> Notices(WorkflowInstance? before, WorkflowInstance after, WorkflowEventType change)
    {
        var notices = EngineNotices.For(before, after, _mirrored, _deskTaskNotified, change);
        _deskTaskNotified |= EngineNotices.NoticedDeskTask(after, notices);
        return [.. notices.Select(static n => n.Command)];
    }

    /// <summary>A linked instance's report to its line (LN-WF1): 'parked', 'resumed' or 'ended', an engine-origin 'effect.line.report'.</summary>
    private IReadOnlyList<EffectCommand> LineReports(WorkflowInstance? before, WorkflowInstance after) =>
        Workflow.Patched(LinkedEventsPatch) && LinkedPhaseReports.For(before, after, _mirrored) is { } report ? [report] : [];

    /// <summary>
    /// A recipe effect's text (P6 unit (c)): the rendered 'next' of the state its transition entered. An engine notice
    /// carries its own text in its arguments, so it has none here.
    /// </summary>
    private string? TextOf(WorkflowInstance after, EffectCommand effect) =>
        effect.Origin == CommandOrigin.Recipe && _recipe is { } recipe ? InstanceViews.Of(after, recipe, _since, timeoutAt: null, ViewContext()).Next : null;

    /// <summary>Waits until every effect the instance emitted has settled (the run's end), surfacing a failed one.</summary>
    private async Task Settle()
    {
        while (_effects.Count > 0)
        {
            Task[] settling = [.. _effects];
            _effects.Clear();
            await Workflow.WhenAllAsync(settling).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Whether an effect's reply may carry facts: the 'forge-reply-facts' marker, read once at the first effect dispatch (after every other marker,
    /// which Begin reads), so a history recorded before it replays with no reply facts exactly as it ran.
    /// </summary>
    private bool ReplyFacts => _replyFacts ??= Workflow.Patched("forge-reply-facts");

    private async Task DispatchOne(WorkflowInstance after, EffectCommand effect)
    {
        var row = new EffectRow(effect.Identity.Text, Workflow.NewGuid().ToString("N"), effect.Effect, effect.Origin, EffectState.Pending, after.Address);
        try
        {
            _ = await Record(row).ConfigureAwait(true);
            if (!await Cleared(row, effect).ConfigureAwait(true))
            {
                return;
            }

            var run = new EffectRun(after.Address.Value, after.Item.Value, after.Attempt, effect, row.Command, TextOf(after, effect), after.Facts, _recipe!.Version, ReplyFacts);   // why: effects run only after Begin set _recipe
            var options = (ActivityOptions)EffectActivities.RunOptions.Clone();
            options.CancellationToken = RunToken(effect);
            options.TaskQueue = Workflow.Info.TaskQueue;   // the instance's queue, from settings (never a constant)
            try
            {
                _ = await Workflow.ExecuteActivityAsync((EffectActivities a) => a.RunAsync(run), options).ConfigureAwait(true);
                _ = _effectsSent.Add(row.Identity);
            }
            catch (ActivityFailureException e) when (e.InnerException is CanceledFailureException && Effects().IsCancellationRequested)
            {
                // The instance was cancelled while the effect retried: it stops there, unsent, and the row says why.
                _ = await Record(row with { State = EffectState.Cancelled, Reason = "cancelled: the instance was cancelled while the effect retried" }).ConfigureAwait(true);
            }
            catch (ActivityFailureException e) when (e.InnerException is not CanceledFailureException)
            {
                // Whatever ended the activity (a setup it can't run in, a refusal at run time, an undeclared reply, spent retries):
                // refused and parked, never a failed workflow. A cancel is the one failure that isn't.
                var (code, reason) = FailureOf(e);
                var failed = await Record(row with { State = EffectState.Refused, Code = code, Reason = reason }).ConfigureAwait(true);
                await Refuse(new OutboxRefused(Dispatcher, new RefusedEffect(effect.Effect, effect.Identity, effect.With, effect.Origin), code, failed.Sequence, reason)).ConfigureAwait(true);
            }
        }
        finally
        {
            _ = _effectsOpen.Remove(row.Identity);
        }
    }

    /// <summary>
    /// What cancels an effect's 'RunEffect': a cancel of the instance stops a recipe effect's retries too, not only its delay. An engine notice (a desk task, a park)
    /// and the report of a cancelled end to its line are never cancelled by it: a cancel Update shares its activation with the resolution of a notice that just
    /// completed, and the server fails the workflow task that cancels an activity it has already completed (ACTIVITY_UNKNOWN) over and over, so the cancel never returned.
    /// Behind the 'engine-effects-survive-cancel' marker, read once at the first effect dispatch like <see cref="ReplyFacts"/>: a history recorded before it asked for the cancel of a running notice and replays exactly so.
    /// </summary>
    private CancellationToken RunToken(EffectCommand effect) =>
        effect.Origin == CommandOrigin.Engine && (_noticesSurviveCancel ??= Workflow.Patched("engine-effects-survive-cancel")) ? Workflow.CancellationToken : IsLineReport(effect) ? Workflow.CancellationToken : Effects().Token;

    /// <summary>The code and message an ended 'RunEffect' is refused with: its own SND code, else SND-WF-015.</summary>
    // Deterministic in practice: the scrub's rules are linear and its result only feeds activity inputs, never a value compared on replay.
    private static (string Code, string Reason) FailureOf(ActivityFailureException failure) => failure.InnerException switch
    {
        ApplicationFailureException { ErrorType: { } type } app when type.StartsWith("SND-", StringComparison.Ordinal) => (type, Scrubbed(app.Message)),
        { } inner => (WorkflowErrors.EffectFailed.Code, Scrubbed(inner.Message)),
        _ => (WorkflowErrors.EffectFailed.Code, Scrubbed(failure.Message)),
    };

    /// <summary>Whatever a block threw can carry a secret: every path into the outbox row and the park question is scrubbed here (the activity scrubs a returned error too).</summary>
    private static string Scrubbed(string reason) => Redactor.Default.Scrub(reason).Text;

    /// <summary>
    /// A recipe effect's undo window and recheck (§5.7): false when a cancel aborted it within the delay (an
    /// 'outbox.cancelled' row; no recheck runs) or the recheck refused it (an 'outbox.refused' row with the code, then
    /// the 'OutboxRefused' trigger carrying the effect, so the park names it and a resume re-sends it).
    /// </summary>
    private async Task<bool> Cleared(EffectRow row, EffectCommand effect)
    {
        if (effect.Origin == CommandOrigin.Recipe && !await Delayed().ConfigureAwait(true))   // an engine notice skips the delay, not the check
        {
            _ = await Record(row with { State = EffectState.Cancelled, Reason = "cancelled: the instance was cancelled within the dispatch delay" }).ConfigureAwait(true);
            return false;
        }

        var recheck = new EffectRecheck(_recipe!.Version, effect.Effect, effect.Origin); // why: effects run only after Begin set _recipe
        var checkedNow = await Workflow.ExecuteLocalActivityAsync((EffectActivities a) => a.RecheckAsync(recheck), EffectActivities.LocalOptions).ConfigureAwait(true);
        if (checkedNow.Code is not { } code)
        {
            return true;
        }

        var refused = await Record(row with { State = EffectState.Refused, Code = code, Reason = checkedNow.Reason }).ConfigureAwait(true);
        await Refuse(new OutboxRefused(Dispatcher, new RefusedEffect(effect.Effect, effect.Identity, effect.With, effect.Origin), code, refused.Sequence)).ConfigureAwait(true);
        return false;
    }

    /// <summary>Waits 'workflow.dispatchDelay'; false when the instance was cancelled first.</summary>
    private async Task<bool> Delayed()
    {
        var cancel = Effects();
        if (cancel.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            await Workflow.DelayAsync(_dispatchDelay, cancel.Token).ConfigureAwait(true);
            return true;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>Hands a recheck refusal to the loop (TransitionDecision's 'outbox.refused' park), unless the instance has ended meanwhile.</summary>
    private async Task Refuse(OutboxRefused refused)
    {
        var ticket = ++_tickets;
        _submitted.Enqueue(new Submission(ticket, refused));
        await Workflow.WaitConditionAsync(() => _decided.ContainsKey(ticket) || _instance is { Ended: true }).ConfigureAwait(true);
        _ = _decided.Remove(ticket);
    }

    private Task<Core.Ledger.LedgerAppend> Record(EffectRow row) =>
        Workflow.ExecuteLocalActivityAsync((EffectActivities a) => a.RecordAsync(new EffectRecord(_start!.Item.Value, row)), EffectActivities.LocalOptions); // why: effects run only after Begin set _start

    /// <summary>The source that aborts effects still in their delay: the instance's cancel, or the workflow's.</summary>
    private CancellationTokenSource Effects() => _effectsCancel ??= CancellationTokenSource.CreateLinkedTokenSource(Workflow.CancellationToken);
}
