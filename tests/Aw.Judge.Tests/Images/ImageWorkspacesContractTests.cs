using Aw.Judge.Core.Images;
using Aw.Judge.Infrastructure.Images;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Images;

[Lane(TestLane.Integration)]
public sealed class ImageWorkspacesContractTests : ImageWorkspacesContract
{
    protected override IImageWorkspaces Create() => new ImageWorkspaces();

    protected override byte[]? Read(IImageWorkspace workspace, string name)
    {
        var path = Path.Combine(workspace.Path, name);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}
