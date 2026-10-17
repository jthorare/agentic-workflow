using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch;

[Lane(TestLane.Unit)]
public sealed class FakeLaunchFilesContractTests : LaunchFilesContract
{
    protected override ILaunchFiles Create() => new FakeLaunchFiles();

    protected override void Open(ILaunchFiles files, LaunchId launch, string proof) => ((FakeLaunchFiles)files).Open(launch, proof);
}
