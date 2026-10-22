using Aw.Kernel.Testing;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.AddRepo;

[Lane(TestLane.Unit)]
public sealed class FakeRepoInspectorContractTests : RepoInspectorContract
{
    protected override ValueTask<(IRepoInspector Inspector, string Checkout, string NotACheckout)> Create() =>
        ValueTask.FromResult<(IRepoInspector, string, string)>((new FakeRepoInspector().Has("/work/demo", git: new RepoGit("main", "abc1234")), "/work/demo", "/work/nowhere"));
}
