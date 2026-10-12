using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Processes;

[Lane(TestLane.Unit)]
public sealed class FakeExecutableLocatorContractTests : ExecutableLocatorContract
{
    protected override IExecutableLocator Create(IReadOnlyList<string> installed) => new FakeExecutableLocator([.. installed]);
}
