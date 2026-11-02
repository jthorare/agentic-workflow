using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.PinConcepts;

[Lane(TestLane.Unit)]
public sealed class FakeProfileIndexWriterContractTests : ProfileIndexWriterContract
{
    protected override (IProfileIndexWriter Writer, ProfileHash Hash) Create()
    {
        var fake = new FakeProfileIndexWriter();
        return (fake, fake.Hash);
    }
}
