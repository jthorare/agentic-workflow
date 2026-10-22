using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks;

/// <summary>The in-memory default-branch port, held to the same contract as the git adapter.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeDefaultBranchContractTests : DefaultBranchContract
{
    protected override ValueTask<(IDefaultBranch Port, string Trunk, string Slashed)> Create() =>
        ValueTask.FromResult<(IDefaultBranch, string, string)>((new FakeDefaultBranch().With("/repo/a", "trunk").With("/repo/b", "release/1"), "/repo/a", "/repo/b"));

    protected override ValueTask<(IDefaultBranch Port, string Checkout)> CreateWithoutDefault() =>
        ValueTask.FromResult<(IDefaultBranch, string)>((new FakeDefaultBranch(), "/repo/none"));
}
