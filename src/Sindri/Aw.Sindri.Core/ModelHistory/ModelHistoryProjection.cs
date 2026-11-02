using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.ModelHistory;

/// <summary>
/// Folds how each model does at each state into the 'model-history' view (model-pick spec §7.2): a 'launch.started' says which model a run used, and the
/// 'step.completed' or 'step.failed' mirrored for that run says how it ended. The fold keeps what it needs to join them in the 'model-runs' view: a registered
/// recipe's version by hash, a started instance's recipe, a launch's model, and the model of a state's latest completed run (so a later transition back into that
/// state charges the run with a redraft). It reads only ledger rows, never Temporal. A run with no launch (a desk or block Step) or a launch that names no model makes no row.
/// </summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class ModelHistoryProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.ModelHistory, ViewNames.ModelRuns];

    public bool Handles(string eventType) =>
        string.Equals(eventType, EventTypes.RecipeRegistered, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.WorkflowStarted, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.LaunchStarted, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.ModelPicked, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.WorkflowTransition, StringComparison.Ordinal);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        return recorded.Envelope.Type switch
        {
            EventTypes.RecipeRegistered => await Registered(recorded, rows, cancellationToken).ConfigureAwait(false),
            EventTypes.WorkflowStarted => await Started(recorded, rows, cancellationToken).ConfigureAwait(false),
            EventTypes.LaunchStarted => await Launched(recorded, rows, cancellationToken).ConfigureAwait(false),
            EventTypes.ModelPicked => await Picked(recorded, rows, cancellationToken).ConfigureAwait(false),
            _ => await Transitioned(recorded, rows, cancellationToken).ConfigureAwait(false),
        };
    }

    // A mirrored row names its instance by the address ('ITM-…/recipe/1'); every key of the fold uses the workflow id ('WFI-…') that a launch.started and a model.picked yield from their address, so the row is read with its instance as that id (#1481).
    private Result<WorkflowEventPayload> Mirrored(LedgerEvent recorded)
    {
        var payload = events.Payload<WorkflowEventPayload>(recorded);
        return payload.IsError || WorkflowInstanceId.TryFrom(payload.Value.Instance, out _) || !InstanceAddress.TryFrom(payload.Value.Instance, out var address)
            ? payload
            : payload.Value with { Instance = WorkflowInstanceId.Of(address).Value };
    }

    private async ValueTask<Result<Unit>> Registered(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var registered = events.Payload<RecipeRegistered>(recorded);
        return registered.IsError
            ? registered.Error
            : await rows.Put(ViewNames.ModelRuns, ModelHistoryKeys.Recipe(registered.Value.Hash.Value), new RunScope(registered.Value.Id, registered.Value.Version), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Started(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var started = Mirrored(recorded);
        if (started.IsError)
        {
            return started.Error;
        }

        var ordered = await rows.Put(ViewNames.ModelRuns, ModelHistoryKeys.Order(started.Value.Instance), new StateOrder([started.Value.To]), cancellationToken).ConfigureAwait(false);
        if (ordered.IsError || started.Value.Detail?.Delta.RecipeHash is not { } hash)
        {
            return ordered;
        }

        var scope = await rows.Get<RunScope>(ViewNames.ModelRuns, ModelHistoryKeys.Recipe(hash), cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        // A recipe the ledger never registered has no version to key a row by: its runs make none
        return scope.Value is null
            ? Unit.Value
            : await rows.Put(ViewNames.ModelRuns, ModelHistoryKeys.Instance(started.Value.Instance), scope.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Launched(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var started = events.Payload<LaunchStarted>(recorded);
        if (started.IsError || started.Value is not { Model: { } model, Instance: { } instance, State: { } state, Attempt: { } attempt })
        {
            return started.IsError ? started.Error : Unit.Value;   // a launch outside an instance, or one that names no model, has nothing to join
        }

        // The run is named by instance, state and attempt, not by launch id: an escalated retry is a new launch of the same run, and the one that ran last owns the outcome
        return await rows.Put(ViewNames.ModelRuns, ModelHistoryKeys.Run(WorkflowInstanceId.Of(instance).Value, state, attempt), new RunModel(model), cancellationToken).ConfigureAwait(false);
    }

    // A pick that escalated from a model: the failed run of that model counts for it, as a run, at the state the pick is for. The failed run wrote no step.failed of its own.
    private async ValueTask<Result<Unit>> Picked(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var picked = events.Payload<ModelPicked>(recorded);
        if (picked.IsError || picked.Value is not { EscalatedFrom: { } from, EscalatedFromKind: { } kind } pick)
        {
            return picked.IsError ? picked.Error : Unit.Value;
        }

        var outcome = ModelOutcomes.OfFailure(kind, reason: null);
        if (outcome is not (ModelOutcome.Refused or ModelOutcome.CheckFailed))
        {
            return Unit.Value;   // only a refused or failed-check run is escalated from
        }

        var scope = await rows.Get<RunScope>(ViewNames.ModelRuns, ModelHistoryKeys.Instance(WorkflowInstanceId.Of(pick.Instance).Value), cancellationToken).ConfigureAwait(false);
        if (scope.IsError || scope.Value is null)
        {
            return scope.IsError ? scope.Error : Unit.Value;
        }

        var at = recorded.Envelope.OccurredAt;
        return await Change(rows, new Slot(scope.Value, pick.State, from), row => row.After(outcome, at), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Transitioned(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var payload = Mirrored(recorded);
        if (payload.IsError)
        {
            return payload.Error;
        }

        var back = await Entered(payload.Value, rows, cancellationToken).ConfigureAwait(false);
        if (back.IsError)
        {
            return back.Error;
        }

        var completed = string.Equals(payload.Value.Trigger, TriggerNames.StepCompleted, StringComparison.Ordinal);
        if (!completed && !string.Equals(payload.Value.Trigger, TriggerNames.StepFailed, StringComparison.Ordinal))
        {
            return Unit.Value;
        }

        var scope = await rows.Get<RunScope>(ViewNames.ModelRuns, ModelHistoryKeys.Instance(payload.Value.Instance), cancellationToken).ConfigureAwait(false);
        if (scope.IsError || scope.Value is null)
        {
            return scope.IsError ? scope.Error : Unit.Value;
        }

        var report = new Report(payload.Value, scope.Value, recorded.Envelope.OccurredAt, back.Value);
        return completed
            ? await Completed(report, rows, cancellationToken).ConfigureAwait(false)
            : await Failed(report, rows, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Result<Unit>> Completed(Report report, IViewRows rows, CancellationToken cancellationToken)
    {
        var (payload, scope, at, _) = report;
        if (report.Back)
        {
            var redrafted = await Redraft(report, rows, cancellationToken).ConfigureAwait(false);
            if (redrafted.IsError)
            {
                return redrafted.Error;
            }
        }

        var model = await ModelOf(payload, rows, cancellationToken).ConfigureAwait(false);
        if (model.IsError || model.Value is null)
        {
            return model.IsError ? model.Error : Unit.Value;
        }

        var counted = await Change(rows, new Slot(scope, payload.From, model.Value.Model), row => row.After(ModelOutcome.Ok, at), cancellationToken).ConfigureAwait(false);
        var consumed = counted.IsError ? counted : await Consume(payload, rows, cancellationToken).ConfigureAwait(false);
        return consumed.IsError
            ? consumed
            : await rows.Put(ViewNames.ModelRuns, ModelHistoryKeys.Completed(payload.Instance, payload.From), model.Value, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Result<Unit>> Failed(Report report, IViewRows rows, CancellationToken cancellationToken)
    {
        var (payload, scope, at, _) = report;
        var model = await ModelOf(payload, rows, cancellationToken).ConfigureAwait(false);
        if (model.IsError || model.Value is null)
        {
            return model.IsError ? model.Error : Unit.Value;
        }

        var outcome = ModelOutcomes.OfFailure(payload.Detail?.FailureKind, payload.Detail?.Reason);
        var counted = await Change(rows, new Slot(scope, payload.From, model.Value.Model), row => row.After(outcome, at), cancellationToken).ConfigureAwait(false);
        return counted.IsError ? counted : await Consume(payload, rows, cancellationToken).ConfigureAwait(false);
    }

    // Records that the transition entered its To state, and says whether it went back: To was first entered before From. The order is per instance, so this is not tied to any recipe.
    private static async ValueTask<Result<bool>> Entered(WorkflowEventPayload payload, IViewRows rows, CancellationToken cancellationToken)
    {
        var key = ModelHistoryKeys.Order(payload.Instance);
        var current = await rows.Get<StateOrder>(ViewNames.ModelRuns, key, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        var order = current.Value?.Entered ?? [];
        var to = order.ToList().IndexOf(payload.To);
        var from = order.ToList().IndexOf(payload.From);
        var back = to >= 0 && from >= 0 && to < from;
        List<string> next = [.. order];
        foreach (var state in new[] { payload.From, payload.To }.Where(state => !next.Contains(state, StringComparer.Ordinal)))
        {
            next.Add(state);
        }

        var put = next.Count == order.Count ? Unit.Value : await rows.Put(ViewNames.ModelRuns, key, new StateOrder(next), cancellationToken).ConfigureAwait(false);
        return put.IsError ? put.Error : back;
    }

    // A transition back into a state whose run completed: that run's model made what the instance is now redoing. Charged once per completed run.
    private static async ValueTask<Result<Unit>> Redraft(Report report, IViewRows rows, CancellationToken cancellationToken)
    {
        var key = ModelHistoryKeys.Completed(report.Payload.Instance, report.Payload.To);
        var earlier = await rows.Get<RunModel>(ViewNames.ModelRuns, key, cancellationToken).ConfigureAwait(false);
        if (earlier.IsError || earlier.Value is null)
        {
            return earlier.IsError ? earlier.Error : Unit.Value;
        }

        var deleted = await rows.Delete(ViewNames.ModelRuns, key, cancellationToken).ConfigureAwait(false);
        return deleted.IsError
            ? deleted
            : await Change(rows, new Slot(report.Scope, report.Payload.To, earlier.Value.Model), static row => row.Redrafted(), cancellationToken).ConfigureAwait(false);
    }

    // The model of the run a report is about: the report's run token carries the attempt, which with the instance and the state names the run the launch.started named.
    private static async ValueTask<Result<RunModel?>> ModelOf(WorkflowEventPayload payload, IViewRows rows, CancellationToken cancellationToken)
    {
        if (payload.Run is null || !RunToken.TryFrom(payload.Run, out var run))
        {
            return Results.Success<RunModel?>(value: null);
        }

        return await rows.Get<RunModel>(ViewNames.ModelRuns, ModelHistoryKeys.Run(payload.Instance, payload.From, run.Number), cancellationToken).ConfigureAwait(false);
    }

    // A report owns its launch's model once: the run number restarts at 1 in every frame entry, so a later run with no launch (a desk redraft) must not find this one's.
    private static async ValueTask<Result<Unit>> Consume(WorkflowEventPayload payload, IViewRows rows, CancellationToken cancellationToken) =>
        payload.Run is not null && RunToken.TryFrom(payload.Run, out var run)
            ? await rows.Delete(ViewNames.ModelRuns, ModelHistoryKeys.Run(payload.Instance, payload.From, run.Number), cancellationToken).ConfigureAwait(false)
            : Unit.Value;

    private static async ValueTask<Result<Unit>> Change(IViewRows rows, Slot slot, Func<ModelHistoryRow, ModelHistoryRow> change, CancellationToken cancellationToken)
    {
        var (scope, state, model) = slot;
        var key = ModelHistoryKeys.For(scope.Recipe, scope.Version, state, model);
        var current = await rows.Get<ModelHistoryRow>(ViewNames.ModelHistory, key, cancellationToken).ConfigureAwait(false);
        return current.IsError
            ? current.Error
            : await rows.Put(ViewNames.ModelHistory, key, change(current.Value ?? ModelHistoryRow.Empty(scope.Recipe, scope.Version, state, model)), cancellationToken).ConfigureAwait(false);
    }

    // A Step report: the mirrored row, the recipe its instance runs, when it was recorded, and whether the transition went back to an earlier state.
    private sealed record Report(WorkflowEventPayload Payload, RunScope Scope, DateTimeOffset At, bool Back);

    // One row of the model-history view: a model at a state of a recipe version.
    private readonly record struct Slot(RunScope Scope, string State, string Model);
}
