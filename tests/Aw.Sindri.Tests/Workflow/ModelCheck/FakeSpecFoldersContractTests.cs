using Aw.Kernel.Testing;
using Aw.Sindri.Core.ModelCheck;

namespace Aw.Sindri.Tests.Workflow.ModelCheck;

/// <summary>The in-memory spec folders honour the spec-folders contract.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeSpecFoldersContractTests : SpecFoldersContract
{
    protected override (ISpecFolders Folders, string Workspace) Arrange(IReadOnlyList<SpecFolder> folders)
    {
        var fake = new FakeSpecFolders();
        foreach (var folder in folders)
        {
            _ = fake.Add(folder);
        }

        return (fake.Strict(), "/work/ws");
    }
}
