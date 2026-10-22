using Aw.Kernel.Testing;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.AddRepo;

[Lane(TestLane.Unit)]
public sealed class FakeProfileRepoWriterContractTests : ProfileRepoWriterContract
{
    protected override IProfileRepoWriter Create() => new FakeProfileRepoWriter();
}
