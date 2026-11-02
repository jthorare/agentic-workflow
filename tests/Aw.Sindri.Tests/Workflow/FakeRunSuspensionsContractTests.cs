using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class FakeRunSuspensionsContractTests : RunSuspensionsContract
{
    protected override ValueTask<IRunSuspensions> Create() => ValueTask.FromResult<IRunSuspensions>(new FakeRunSuspensions());

    protected override ValueTask Run(IRunSuspensions subject, LaunchId launch) => ValueTask.CompletedTask;

    protected override ValueTask Suspend(IRunSuspensions subject, LaunchId launch)
    {
        ((FakeRunSuspensions)subject).Suspend(launch);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask Resume(IRunSuspensions subject, LaunchId launch)
    {
        ((FakeRunSuspensions)subject).Resume(launch);
        return ValueTask.CompletedTask;
    }
}
