using Aw.Judge.Core.Images;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Images;

[Lane(TestLane.Unit)]
public sealed class FakeImageWorkspacesContractTests : ImageWorkspacesContract
{
    protected override IImageWorkspaces Create() => new FakeImageWorkspaces();

    protected override byte[]? Read(IImageWorkspace workspace, string name) => ((FakeImageWorkspace)workspace).Read(name);
}
