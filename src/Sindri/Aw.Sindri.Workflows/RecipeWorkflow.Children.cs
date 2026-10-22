using System.Globalization;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Children, both ways (engine P7 plan D1, D3, D6, D10; spec §5.8). A child wait's entry starts the child as a Temporal child
/// workflow at its address; the child signals its park, resume and escalation to its parent ('child'), and its end comes back as
/// its workflow result, which the parent turns into the wait's satisfaction with the child's declared outputs; a parent's ending
/// signals its live children to cancel ('parentCancel'), so each records its own 'cancelled' and frees its claim. A history from
/// before engine P7 (no 'p7-children' marker) starts no child, as recorded.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    private const string Inbox = "service:workflow.inbox";
    private static readonly TimeSpan _childrenGrace = TimeSpan.FromSeconds(30);
    private readonly Dictionary<WorkflowInstanceId, ChildRun> _childRuns = [];
    private readonly List<ChildChanged> _childHeld = [];

    /// <summary>A child's park, resume or escalation (spec §5.8): the loop decides it at the instance's next wait.</summary>
    /// <param name="change">What the child reported.</param>
    /// <returns>Done once queued.</returns>
    [WorkflowSignal("child")]
    public Task ChildAsync(ChildChanged change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (_childRuns.TryGetValue(change.Child, out var run))
        {
            run.State = change.Change switch
            {
                ChildChange.Parked => "parked",
                ChildChange.Escalated => "escalated",
                ChildChange.Resumed or _ => "running",
            };
        }

        _submitted.Enqueue(new Submission(Ticket: 0, change));
        return Task.CompletedTask;
    }

    /// <summary>The cancel a parent's ending sends its live children (spec §5.8, D6): an Update can't be sent from workflow code, so it is a signal that submits the same trigger.</summary>
    /// <param name="cancel">The cancel, origin parent.</param>
    /// <returns>Done once queued.</returns>
    [WorkflowSignal("parentCancel")]
    public Task ParentCancelAsync(CancelRequested cancel)
    {
        ArgumentNullException.ThrowIfNull(cancel);
        _submitted.Enqueue(new Submission(Ticket: 0, cancel));
        return Task.CompletedTask;
    }

    /// <summary>The pinned children of the recipe ('LoadChildren', a local activity); none for a recipe with no child wait or a history from before engine P7.</summary>
    private async Task<ChildPins> LoadChildPins(LoadedRecipe loaded)
    {
        if (!_children || !RecipeChildren.Of(loaded.Recipe).Any())
        {
            return ChildPins.None;
        }

        var pinned = await Workflow.ExecuteLocalActivityAsync(
            (ChildActivities a) => a.LoadChildrenAsync(loaded.Version),
            new LocalActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(30), RetryPolicy = new RetryPolicy { MaximumAttempts = 3 } }).ConfigureAwait(true);
        return new ChildPins(pinned);
    }

    /// <summary>Starts every child the decision created (a child wait's entry, W17), right after the entry's event is in the ledger.</summary>
    private void Spawn(Decision? decision)
    {
        if (!_children || decision is not Applied applied || _instance is not { } parent)
        {
            return;
        }

        foreach (var change in applied.Changes.Where(c => c.Event.Type == WorkflowEventType.Started && c.After.Parent == parent.Id))
        {
            StartChild(change.After);
        }
    }

    private void StartChild(WorkflowInstance child)
    {
        var pinned = child.Recipe;
        var input = new InstanceStart(
            new RecipeVersion(pinned.Id, pinned.Version, pinned.Hash),
            child.Item,
            child.Attempt,
            child.Facts.ToDictionary(static f => f.Key, static f => new InputFact(f.Key.Value, f.Value)),
            child.Tenant,
            child.Ring)
        {
            ChildAddress = child.Address,
            ParentId = child.Parent,
            StartedAt = Now,
        };
        var started = Workflow.StartChildWorkflowAsync("RecipeWorkflow", [input], new ChildWorkflowOptions { Id = child.Address.Value, ParentClosePolicy = ParentClosePolicy.Terminate });
        var run = new ChildRun(child.Id, child.Address, started);
        _childRuns[child.Id] = run;
        _ = Deliver(run);
    }

    /// <summary>Waits for the child's end and delivers it to the parent's wait with the child's outputs (D3, D4); a child that fails or is cancelled from outside is 'cancelled'.</summary>
    private async Task Deliver(ChildRun run)
    {
        var outcome = EngineStates.Cancelled;
        IReadOnlyDictionary<FactKey, FactValue> outputs = new Dictionary<FactKey, FactValue>();
        try
        {
            var handle = await run.Started.ConfigureAwait(true);
            var result = await handle.GetResultAsync<RecipeResult>().ConfigureAwait(true);
            outcome = result.Outcome ?? outcome;
            outputs = result.Outputs ?? outputs;
        }
        catch (Exception failed) when (IsActivityFailure(failed))
        {
            outcome = EngineStates.Cancelled;   // boundary: the child was terminated, failed or never started; the parent's wait hears it as a cancel
        }
        catch (Exception failed) when (TemporalException.IsCanceledException(failed))
        {
            run.Done = true;   // this workflow is being cancelled: nothing is left to deliver to
            return;
        }

        run.Done = true;
        _ = _childRuns.Remove(run.Id);
        _delivered.Enqueue(new WaitSatisfied(Inbox, new ChildWaitSource(run.Id, TriggerKey.For(run.Id, "end")), OutcomeName.From(outcome), outputs));
        _arrivals++;
    }

    /// <summary>Tells the parent about a change in this child's suspension (park, escalation, resume); a root instance tells nobody.</summary>
    private void Announce(WorkflowInstance? before, WorkflowInstance after)
    {
        if (!_children || Workflow.Info.Parent is not { } parent || after.Ended || ChangeOf(before?.Suspension, after.Suspension) is not { } kind)
        {
            return;
        }

        var open = after.Suspension;
        var change = new ChildChanged(Inbox, after.Id, kind, open?.Reason, open?.Question, TriggerKey.For(after.Id, string.Create(CultureInfo.InvariantCulture, $"child-{_mirrored}")));
        _effects.Add(Tell(parent.WorkflowId, change));
    }

    private static ChildChange? ChangeOf(Suspension? before, Suspension? after)
    {
        var (was, now) = (Shape(before), Shape(after));
        if (now is null)
        {
            return was is not null && after is null ? ChildChange.Resumed : null;
        }

        return was == now ? null : now.Value.Change;
    }

    private static (ChildChange Change, ParkReason? Reason, string Question)? Shape(Suspension? suspension) => suspension?.Kind switch
    {
        SuspensionKind.Park => (ChildChange.Parked, suspension.Reason, suspension.Question),
        SuspensionKind.Escalation => (ChildChange.Escalated, suspension.Reason, suspension.Question),
        SuspensionKind.Takeover or null or _ => null,
    };

    private static async Task Tell(string parent, ChildChanged change)
    {
        try
        {
            await Workflow.GetExternalWorkflowHandle<RecipeWorkflow>(parent).SignalAsync(wf => wf.ChildAsync(change)).ConfigureAwait(true);
        }
        catch (Exception failed) when (IsActivityFailure(failed))
        {
            // boundary: the parent has gone; a child that outlives its parent is terminated with it
        }
    }

    /// <summary>A child change the parent couldn't take (busy with another suspension) stays until the parent changes (spec §5.8).</summary>
    private void Hold(Submission submission, Decision decision)
    {
        if (decision is Rejected && submission.Trigger is ChildChanged held)
        {
            _childHeld.Add(held);
        }
    }

    /// <summary>Gives the held child changes another try after the parent changed.</summary>
    private void Retry()
    {
        foreach (var held in _childHeld)
        {
            _submitted.Enqueue(new Submission(Ticket: 0, held));
        }

        _childHeld.Clear();
    }

    /// <summary>The children still live when this instance ends are cancelled (D6); they get a grace period to record their own cancel before the parent-close terminate backstops them.</summary>
    private async Task CancelChildren(WorkflowInstance ended)
    {
        if (!_children || _childRuns.Count == 0)
        {
            return;
        }

        var cancel = new CancelRequested(Inbox, CancelOrigin.Parent, Stamp: null, Sequence: null, ended.Id);
        var live = _childRuns.Values.ToList();
        foreach (var run in live)
        {
            try
            {
                var handle = await run.Started.ConfigureAwait(true);
                await handle.SignalAsync("parentCancel", [cancel]).ConfigureAwait(true);
            }
            catch (Exception failed) when (IsActivityFailure(failed))
            {
                run.Done = true;   // boundary: a child that never started or already ended needs no cancel
            }
        }

        _ = await Workflow.WaitConditionAsync(() => live.TrueForAll(static r => r.Done), _childrenGrace).ConfigureAwait(true);
    }

    /// <summary>The event's detail with the mirrored child's address in its suspension, so a needs-you row names the child (the id alone doesn't give the address back).</summary>
    private WorkflowEventDetail Located(WorkflowEventDetail detail, WorkflowInstance after) =>
        _children && after.Suspension?.Mirrors is { } child && _childRuns.TryGetValue(child, out var run) && detail.Delta.Suspension is { } summary
            ? detail with { Delta = detail.Delta with { Suspension = summary with { MirrorsAddress = run.Address.Value } } }
            : detail;

    private IReadOnlyList<ChildView>? ChildViews() => _children ? [.. _childRuns.Values.Select(static r => new ChildView(r.Address, r.State))] : null;

    private static DateTimeOffset? StartedAtOf(InstanceStart? start) => start is { StartedAt: var at } && at != default ? at : null;

    private InstanceViewContext ViewContext() => new(_mirrored, _provenance, ChildViews(), _subject, _contents, _injection, StartedAtOf(_start), _start?.StartedBy);

    /// <summary>What a child hands its parent (DT spec §8.4): its declared outputs. A root instance's result carries none; neither does a history from before engine P7.</summary>
    private Dictionary<FactKey, FactValue>? OutputsOf(WorkflowInstance ended)
    {
        if (!_children || Workflow.Info.Parent is null)
        {
            return null;
        }

        var recipe = _context!.Recipe;   // why: an instance that ended has begun
        return ChildResults.Outputs(recipe, ended).ToDictionary();
    }
}
