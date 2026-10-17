using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

[Lane(TestLane.Unit)]
public sealed class FakeContainerRuntimeContractTests : ContainerRuntimeContract
{
    protected override IContainerRuntime Create(ContainerRuntimeFacts facts, IReadOnlyDictionary<string, string[]> localImages)
    {
        var runtime = new FakeContainerRuntime(facts);
        foreach (var (image, digests) in localImages)
        {
            runtime.LocalImages[image] = digests;
        }

        return runtime;
    }
}
