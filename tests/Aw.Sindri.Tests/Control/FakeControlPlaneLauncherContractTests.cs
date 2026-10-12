using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Control;

[Lane(TestLane.Unit)]
public sealed class FakeControlPlaneLauncherContractTests : ControlPlaneLauncherContract
{
    protected override IControlPlaneLauncher ExitingWith(string output) => FakeControlPlaneLauncher.ExitingWith(output);
}
