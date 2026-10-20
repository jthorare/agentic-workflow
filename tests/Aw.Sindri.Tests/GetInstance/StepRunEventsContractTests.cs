using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>The in-memory step-run source keeps the contract.</summary>
[Lane(TestLane.Unit)]
public sealed class StepRunEventsContractTests : StepRunSourceContract
{
    protected override ValueTask<StepRunRead> Create(IReadOnlyList<(InstanceAddress Address, RecordedWorkflowEvent Event)> rows) =>
        ValueTask.FromResult<StepRunRead>(StepRunEvents.Of([.. rows]).EventsOf);

    protected override ValueTask<StepRunRead> CreateFailing() => ValueTask.FromResult<StepRunRead>(StepRunEvents.Failing().EventsOf);
}
