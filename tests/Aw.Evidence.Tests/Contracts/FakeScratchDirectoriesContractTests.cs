using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeScratchDirectoriesContractTests : ScratchDirectoriesContract
{
    protected override Task<ScratchUnderTest> ArrangeAsync()
    {
        var fake = new FakeScratchDirectories();
        return Task.FromResult(new ScratchUnderTest(fake, fake.Exists));
    }
}
