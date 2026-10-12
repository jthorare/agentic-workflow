using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Redaction;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Aw.Redaction;

namespace Aw.Judge.Tests.Redaction;

[Lane(TestLane.Unit)]
public sealed class AwInputRedactorContractTests : InputRedactorContract
{
    protected override IInputRedactor Create() => new AwInputRedactor(Redactor.Default);
}
