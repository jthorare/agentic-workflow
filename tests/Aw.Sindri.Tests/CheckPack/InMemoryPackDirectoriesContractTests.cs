using Aw.Kernel.Testing;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Testing.LensPacks;

namespace Aw.Sindri.Tests.CheckPack;

/// <summary>The in-memory pack directories, held to the same contract as the disk adapter.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryPackDirectoriesContractTests : PackDirectoriesContract
{
    protected override ValueTask<(IPackDirectories Port, string Directory)> Create() =>
        ValueTask.FromResult<(IPackDirectories, string)>((new InMemoryPackDirectories().With("/packs/p", new Dictionary<string, string>(StringComparer.Ordinal) { ["pack.json"] = "{}", ["lenses/a.md"] = "alpha" }, "lenses/out.md"), "/packs/p"));

    protected override ValueTask<(IPackDirectories Port, string Directory)> CreateMissing() =>
        ValueTask.FromResult<(IPackDirectories, string)>((new InMemoryPackDirectories(), "/packs/none"));
}
