using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Redaction;

[Lane(TestLane.Unit)]
public sealed class FakeInputRedactorContractTests : InputRedactorContract
{
    protected override IInputRedactor Create() => new FakeInputRedactor();
}
