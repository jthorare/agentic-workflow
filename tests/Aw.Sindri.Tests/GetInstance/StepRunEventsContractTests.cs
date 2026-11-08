using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>The in-memory step-run source keeps the contract.</summary>
[Lane(TestLane.Unit)]
public sealed class StepRunEventsContractTests : StepRunSourceContract
{
    protected override ValueTask<StepRunRead> Create(IReadOnlyList<(InstanceAddress Address, StepRunRow Row)> rows) =>
        ValueTask.FromResult<StepRunRead>(StepRunEvents.Of([.. rows.Select(static r => (r.Address, r.Row.ToEvent()))]).EventsOf);

    protected override ValueTask<StepRunRead> CreateFailing() => ValueTask.FromResult<StepRunRead>(StepRunEvents.Failing().EventsOf);
}
