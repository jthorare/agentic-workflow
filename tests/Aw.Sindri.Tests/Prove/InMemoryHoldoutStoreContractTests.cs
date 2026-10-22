using Aw.Kernel.Testing;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Testing.Prove;

namespace Aw.Sindri.Tests.Prove;

/// <summary>The in-memory holdout store honours the holdout store contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryHoldoutStoreContractTests : HoldoutStoreContract
{
    protected override IHoldoutStore Create() => new InMemoryHoldoutStore();

    protected override void Seed(IHoldoutStore store, string line, string phase, string? setText, string? sealText) =>
        ((InMemoryHoldoutStore)store).Put(line, phase, setText, sealText);
}
