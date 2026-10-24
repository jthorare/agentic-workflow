using Aw.Evidence.Infrastructure.Desktop;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Integration)]
public sealed class TempScratchDirectoriesContractTests : ScratchDirectoriesContract
{
    protected override Task<ScratchUnderTest> ArrangeAsync() => Task.FromResult(new ScratchUnderTest(new TempScratchDirectories(), Directory.Exists));
}
