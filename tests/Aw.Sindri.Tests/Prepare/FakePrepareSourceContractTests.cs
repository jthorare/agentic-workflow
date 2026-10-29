using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Testing.Prepare;

namespace Aw.Sindri.Tests.Prepare;

[Lane(TestLane.Unit)]
public sealed class FakePrepareSourceContractTests : PrepareSourceContract
{
    protected override ValueTask<(IPrepareSource Port, string Repo, string First, string Second)> Scenario() =>
        ValueTask.FromResult<(IPrepareSource, string, string, string)>((new FakePrepareSource().With("c1", Lockfile, "LOCK-A").With("c2", Lockfile, "LOCK-B"), "/work/repo", "c1", "c2"));
}
