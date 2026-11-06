using Aw.Kernel.Testing;
using Aw.Sindri.Core.ModelCheck;
using Aw.Sindri.Infrastructure.ModelCheck;

namespace Aw.Sindri.Tests.Workflow.ModelCheck;

/// <summary>The spec folders read from disk honour the spec-folders contract, and never follow a link.</summary>
[Lane(TestLane.Integration)]
public sealed class DiskSpecFoldersContractTests : SpecFoldersContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aw-spec-folders-" + Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    protected override (ISpecFolders Folders, string Workspace) Arrange(IReadOnlyList<SpecFolder> folders)
    {
        var workspace = Path.Combine(_root, "ws");
        _ = Directory.CreateDirectory(workspace);
        var secret = Path.Combine(_root, "outside.txt");
        File.WriteAllText(secret, "text a link must not hand over");
        foreach (var folder in folders)
        {
            foreach (var file in folder.Files)
            {
                var path = Path.Combine(workspace, folder.Path, file.Name);
                _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);   // why: a path built from a folder and a file name always has a parent
                switch (file.Kind)
                {
                    case SpecFileKind.Symlink:
                        _ = File.CreateSymbolicLink(path, secret);
                        break;
                    case SpecFileKind.Other:
                        _ = Directory.CreateDirectory(path);
                        break;
                    case SpecFileKind.Regular:
                    default:
                        File.WriteAllText(path, file.Text);
                        break;
                }
            }
        }

        return (new DiskSpecFolders(), workspace);
    }
}
