using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeExecutableLocatorContractTests : ExecutableLocatorContract
{
    private const string Root = "/fake/root";

    protected override Task<LocatorUnderTest> ArrangeAsync() =>
        Task.FromResult(new LocatorUnderTest(
            new FakeExecutableLocator()
                .Executable(Root + "/bin/tool")
                .Executable(Root + "/link-to-tool", Root + "/bin/tool")
                .Executable(Root + "/link-to-bin/tool", Root + "/bin/tool")
                .Executable(Root + "/outside/other")
                .File(Root + "/bin/notes.txt")
                .File(Root + "/link-to-tool", Root + "/bin/tool")
                .Directory(Root + "/bin")
                .Directory(Root + "/link-to-bin", Root + "/bin"),
            Root));
}
