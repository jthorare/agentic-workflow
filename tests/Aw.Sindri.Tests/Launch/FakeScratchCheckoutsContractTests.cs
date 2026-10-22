using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch;

[Lane(TestLane.Unit)]
public sealed class FakeScratchCheckoutsContractTests : ScratchCheckoutsContract
{
    private readonly FakeScratchCheckouts _fake = new();

    protected override IScratchCheckouts Create(out string repoPath, out (string First, string Second) commits)
    {
        repoPath = "/work/repo";
        commits = ("a1b2c3d", "e4f5a6b");
        return _fake;
    }

    protected override string? HeadAt(string path) => _fake.Checkouts.GetValueOrDefault(path);

    protected override string PathFor(string name) => "/scratch/" + name;
}
