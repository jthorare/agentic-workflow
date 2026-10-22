using Aw.Kernel.Testing;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Setup;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.AddRepo;

[Lane(TestLane.Integration)]
public sealed class GitRepoInspectorContractTests : RepoInspectorContract, IDisposable
{
    private readonly TempHome _home = new();
    private TempGitRepo? _repo;

    public void Dispose()
    {
        _repo?.Dispose();
        _home.Dispose();
    }

    protected override async ValueTask<(IRepoInspector Inspector, string Checkout, string NotACheckout)> Create()
    {
        _repo = await TempGitRepo.Create();
        var outside = Path.Combine(_home.Path, "outside");
        _ = Directory.CreateDirectory(outside);
        return (new GitRepoInspector(new SindriSettings(Path.Combine(_home.Path, "sindri"), Path.Combine(_home.Path, "sindri", "profile.yaml"))), RealPath.Of(_repo.Path)!, outside);
    }
}
