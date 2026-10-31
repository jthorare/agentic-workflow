using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.PinConcepts;

[Lane(TestLane.Unit)]
public sealed class FakeProfileConceptWriterContractTests : ProfileConceptWriterContract
{
    protected override (IProfileConceptWriter Writer, ProfileHash Hash) Create()
    {
        var fake = new FakeProfileConceptWriter();
        return (fake, fake.Hash);
    }
}
