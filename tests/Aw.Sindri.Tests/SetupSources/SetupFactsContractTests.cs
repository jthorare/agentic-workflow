using Aw.Kernel.Testing;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.SetupSources;

[Lane(TestLane.Unit)]
public sealed class SetupFactsContractTests : SetupFactsContract, IAsyncDisposable
{
    private readonly SetupFactsRig _rig = new();

    public async ValueTask DisposeAsync() => await _rig.DisposeAsync();

    protected override ISetupFacts Create() => _rig.Facts;
}
