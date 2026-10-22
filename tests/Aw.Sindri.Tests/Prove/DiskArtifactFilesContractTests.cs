using Aw.Kernel.Testing;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Infrastructure.Prove;
using Aw.Sindri.Testing.Prove;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Prove;

/// <summary>The disk artifact files honour the artifact files contract, and refuse a link that leaves the worktree.</summary>
[Lane(TestLane.Integration)]
public sealed class DiskArtifactFilesContractTests : ArtifactFilesContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override string Workspace => Path.Combine(_home.Path, "phase-1");

    protected override IArtifactFiles Create() => new DiskArtifactFiles();

    protected override void Put(string path, string text)
    {
        var file = Path.Combine(Workspace, path);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
    }

    [Fact]
    public async Task A_link_to_a_file_outside_the_worktree_is_refused()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var outside = Path.Combine(_home.Path, "secret.txt");
        await File.WriteAllTextAsync(outside, "secret", TestContext.Current.CancellationToken);
        Put("keep.txt", "x");
        _ = File.CreateSymbolicLink(Path.Combine(Workspace, "link.txt"), outside);

        var read = await Create().Sha256(Workspace, "link.txt", TestContext.Current.CancellationToken);

        Assert.True(read.IsError);
    }
}
