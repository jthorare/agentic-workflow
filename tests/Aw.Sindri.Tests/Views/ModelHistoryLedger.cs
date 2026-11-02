using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The ledger rows of a model-history scenario, recorded in order, for the tests that fold them through the SQLite views store.</summary>
/// <param name="events">Builds the envelopes.</param>
internal sealed class ModelHistoryLedger(EventFactory events)
{
    private readonly List<LedgerEvent> _rows = [];

    public IReadOnlyList<LedgerEvent> Rows => _rows;

    public ModelHistoryLedger Begin(string state = "draft")
    {
        _ = Add(EventTypes.RecipeRegistered, new RecipeRegistered(ModelHistoryFolds.Recipe, ModelHistoryFolds.Version, ModelHistoryFolds.Hash));
        _ = Add(EventTypes.WorkflowStarted, ModelHistoryFolds.Payload("", state, "start", run: null, reason: null, started: true));
        return this;
    }

    public ModelHistoryLedger Launched(string run, string model, string state = "draft")
    {
        _rows.Add(Recorded.Of(events, EventTypes.LaunchStarted, ModelHistoryFolds.LaunchPayload(run, state, model), _rows.Count + 1L, A.FirstItemId));
        return this;
    }

    public ModelHistoryLedger Completed(string from, string to, string run) =>
        _ = Add(EventTypes.WorkflowTransition, ModelHistoryFolds.Payload(from, to, TriggerNames.StepCompleted, run, reason: null, started: false));

    public ModelHistoryLedger Failed(string state, string run, string reason) =>
        _ = Add(EventTypes.WorkflowTransition, ModelHistoryFolds.Payload(state, state, TriggerNames.StepFailed, run, reason, started: false));

    private ModelHistoryLedger Add<TPayload>(string type, TPayload payload)
    {
        _rows.Add(Recorded.Of(events, type, payload, _rows.Count + 1L, A.FirstItemId));
        return this;
    }
}
