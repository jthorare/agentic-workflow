using Aw.Kernel.Testing;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Infrastructure.LensPacks;
using Aw.Sindri.Testing.LensPacks;

namespace Aw.Sindri.Tests.CheckPack;

/// <summary>The disk pack directories against a real temp directory, with a link that points outside it.</summary>
[Lane(TestLane.Integration)]
public sealed class DiskPackDirectoriesContractTests : PackDirectoriesContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aw-pack-dir-" + RealTime.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    protected override async ValueTask<(IPackDirectories Port, string Directory)> Create()
    {
        var pack = Path.Combine(_root, "pack");
        _ = Directory.CreateDirectory(Path.Combine(pack, "lenses"));
        await File.WriteAllTextAsync(Path.Combine(pack, "pack.json"), "{}", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(pack, "lenses", "a.md"), "alpha", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_root, "secret.txt"), "outside", TestContext.Current.CancellationToken);
        _ = File.CreateSymbolicLink(Path.Combine(pack, "lenses", "out.md"), Path.Combine(_root, "secret.txt"));
        return (new DiskPackDirectories(), pack);
    }

    protected override ValueTask<(IPackDirectories Port, string Directory)> CreateMissing() =>
        ValueTask.FromResult<(IPackDirectories, string)>((new DiskPackDirectories(), Path.Combine(_root, "none")));
}
