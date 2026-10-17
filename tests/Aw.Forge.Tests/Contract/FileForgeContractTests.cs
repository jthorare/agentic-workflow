using Aw.Forge.Testing;

namespace Aw.Forge.Tests.Contract;

[Trait("Lane", "integration")]
public sealed class FileForgeContractTests : ForgeContract, IDisposable
{
    private readonly List<DirectoryInfo> _directories = [];

    protected override ForgeUnderTest Create()
    {
        var directory = Directory.CreateTempSubdirectory("aw-forge-contract-");
        _directories.Add(directory);
        return ForgeWorlds.File(directory.FullName);
    }

    public void Dispose()
    {
        foreach (var directory in _directories)
        {
            directory.Delete(recursive: true);
        }
    }
}
